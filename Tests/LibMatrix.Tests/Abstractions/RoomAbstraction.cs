using ArcaneLibs.Extensions;
using LibMatrix.EventTypes.Spec.State.RoomInfo;
using LibMatrix.EventTypes.Spec.State.Space;
using LibMatrix.Homeservers;
using LibMatrix.Responses;
using LibMatrix.RoomTypes;

namespace LibMatrix.Tests.Abstractions;

public static class RoomAbstraction {
    public static async Task<GenericRoom> GetTestRoom(AuthenticatedHomeserverGeneric hs) {
        var crq = new CreateRoomRequest() {
            Name = "LibMatrix Test Room",
            // Visibility = CreateRoomVisibility.Public,
            RoomAliasName = Guid.NewGuid().ToString()
        };
        crq.InitialState ??= new List<StateEvent>();
        crq.InitialState.Add(new StateEvent() {
            Type = RoomTopicEventContent.EventId,
            StateKey = "",
            TypedContent = new RoomTopicEventContent() {
                Topic = "LibMatrix Test Room " + DateTime.Now.ToString("O")
            }
        });
        crq.InitialState.Add(new StateEvent() {
            Type = RoomNameEventContent.EventId,
            StateKey = "",
            TypedContent = new RoomNameEventContent() {
                Name = "LibMatrix Test Room " + DateTime.Now.ToString("O")
            }
        });
        crq.InitialState.Add(new StateEvent() {
            Type = RoomAvatarEventContent.EventId,
            StateKey = "",
            TypedContent = new RoomAvatarEventContent() {
                Url = "mxc://conduit.rory.gay/r9KiT0f9eQbv8pv4RxwBZFuzhfKjGWHx"
            }
        });
        crq.InitialState.Add(new StateEvent() {
            Type = RoomAliasEventContent.EventId,
            StateKey = "",
            TypedContent = new RoomAliasEventContent() {
                Aliases = Enumerable
                    .Range(0, 100)
                    .Select(_ => $"#{Guid.NewGuid()}:matrixunittests.rory.gay").ToList()
            }
        });
        var testRoom = await hs.CreateRoom(crq);

        await testRoom.SendStateEventAsync("gay.rory.libmatrix.unit_test_room", new object());

        return testRoom;
    }

    private static SemaphoreSlim _spaceSemaphore = new(1, 1);

    public static async Task<SpaceRoom> GetTestSpace(AuthenticatedHomeserverGeneric hs, int roomCount = 100, bool addSpaces = false, int spaceSizeReduction = 10) {
        _spaceSemaphore ??= new SemaphoreSlim(roomCount / spaceSizeReduction, roomCount / spaceSizeReduction);
        var crq = new CreateRoomRequest() {
            Name = $"LibMatrix Test Space ({roomCount} children)",
            // Visibility = CreateRoomVisibility.Public,
            RoomAliasName = Guid.NewGuid().ToString(),
            InitialState = new List<StateEvent>()
        };
        crq.CreationContentBaseType.Type = "m.space";

        var createRoomTasks = Enumerable.Range(0, roomCount)
            .Select(_ => hs.CreateRoom(new CreateRoomRequest() {
                Name = $"LibMatrix Test Room {Guid.NewGuid()}",
                // Visibility = CreateRoomVisibility.Public,
                RoomAliasName = Guid.NewGuid().ToString()
            })).ToAsyncEnumerable();

        await foreach (var room in createRoomTasks)
            crq.InitialState.Add(new StateEvent {
                Type = "m.space.child",
                StateKey = room.RoomId,
                TypedContent = new SpaceChildEventContent() {
                    Via = new List<string> {
                        room.RoomId.Split(":")[1]
                    }
                }
            });

        if (addSpaces)
            for (var i = 0; i < roomCount; i++) {
                var space = await GetTestSpace(hs, roomCount - spaceSizeReduction, true, spaceSizeReduction);
                crq.InitialState.Add(new StateEvent {
                    Type = "m.space.child",
                    StateKey = space.RoomId,
                    TypedContent = new SpaceChildEventContent() {
                        Via = new List<string> {
                            space.RoomId.Split(":")[1]
                        }
                    }
                });
            }

        var testSpace = (await hs.CreateRoom(crq)).AsSpace();

        await testSpace.SendStateEventAsync("gay.rory.libmatrix.unit_test_room", new object());

        // _spaceSemaphore.Release();
        return testSpace;
    }
}