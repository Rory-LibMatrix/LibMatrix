using ArcaneLibs.Extensions;
using LibMatrix.EventTypes.Spec.State.Space;
using LibMatrix.Responses;
using LibMatrix.RoomTypes;
using LibMatrix.Tests.Abstractions;
using LibMatrix.Tests.Fixtures;
using Xunit.Abstractions;
using Xunit.Microsoft.DependencyInjection.Abstracts;

namespace LibMatrix.Tests.Tests;

public class SpaceTests : TestBed<TestFixture> {
    private readonly HomeserverAbstraction _hsAbstraction;

    public SpaceTests(ITestOutputHelper testOutputHelper, TestFixture fixture) : base(testOutputHelper, fixture) {
        _hsAbstraction = _fixture.GetService<HomeserverAbstraction>(_testOutputHelper) ?? throw new InvalidOperationException($"Failed to get {nameof(HomeserverAbstraction)}");
    }

    [Fact]
    public async Task AddChildAsync() {
        var hs = await _hsAbstraction.GetConfiguredHomeserver();
        var crq = new CreateRoomRequest() {
            Name = "Test space"
        };
        crq.CreationContent["type"] = SpaceRoom.TypeName;
        var space = (await hs.CreateRoom(crq)).AsSpace();

        var child = await hs.CreateRoom(new CreateRoomRequest() {
            Name = "Test child"
        });

        await space.AddChildAsync(child);

        //validate children
        var children = space.GetChildrenAsync().ToBlockingEnumerable().ToList();
        Assert.NotNull(children);
        Assert.NotEmpty(children);
        Assert.Single(children, x => x.RoomId == child.RoomId);
    }

    [Fact]
    public async Task AddChildByIdAsync() {
        var hs = await _hsAbstraction.GetConfiguredHomeserver();
        var crq = new CreateRoomRequest() {
            Name = "Test space"
        };
        crq.CreationContent["type"] = SpaceRoom.TypeName;
        var space = (await hs.CreateRoom(crq)).AsSpace();

        var child = await hs.CreateRoom(new CreateRoomRequest() {
            Name = "Test child"
        });

        await space.AddChildByIdAsync(child.RoomId);

        //validate children
        var children = space.GetChildrenAsync().ToBlockingEnumerable().ToList();
        Assert.NotNull(children);
        Assert.NotEmpty(children);
        Assert.Single(children, x => x.RoomId == child.RoomId);
    }

    [Fact]
    public async Task GetChildrenAsync() {
        var hs = await _hsAbstraction.GetConfiguredHomeserver();
        var expectedChildren = Enumerable.Range(0, 10).Select(async _ => {
            var room = await hs.CreateRoom(new CreateRoomRequest() {
                Name = "Test child"
            });
            return room;
        }).ToAsyncResultEnumerable().ToBlockingEnumerable().ToList();

        var crq = new CreateRoomRequest() {
            Name = "Test space",
            InitialState = expectedChildren.Select(c => new MatrixEvent() {
                Type = "m.space.child",
                StateKey = c.RoomId,
                TypedContent = new SpaceChildEventContent() {
                    Via = new List<string> {
                        c.RoomId.Split(":")[1]
                    }
                }
            }).ToList()
        };
        crq.CreationContent["type"] = SpaceRoom.TypeName;
        var space = (await hs.CreateRoom(crq)).AsSpace();

        var children = space.GetChildrenAsync().ToBlockingEnumerable().ToList();
        Assert.NotNull(children);
        Assert.NotEmpty(children);
        Assert.Equal(expectedChildren.Count, children.Count);
        foreach (var expectedChild in expectedChildren) {
            Assert.Single(children, x => x.RoomId == expectedChild.RoomId);
        }
    }
}