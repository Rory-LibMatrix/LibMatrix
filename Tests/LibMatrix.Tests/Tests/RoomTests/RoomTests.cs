using System.Text;
using ArcaneLibs.Extensions;
using LibMatrix.EventTypes.Spec;
using LibMatrix.EventTypes.Spec.State.RoomInfo;
using LibMatrix.Responses;
using LibMatrix.Tests.Abstractions;
using LibMatrix.Tests.Fixtures;
using Xunit.Abstractions;
using Xunit.Microsoft.DependencyInjection.Abstracts;

namespace LibMatrix.Tests.Tests;

public class RoomTests : TestBed<TestFixture> {
    private readonly HomeserverAbstraction _hsAbstraction;

    public RoomTests(ITestOutputHelper testOutputHelper, TestFixture fixture) : base(testOutputHelper, fixture) {
        _hsAbstraction = _fixture.GetService<HomeserverAbstraction>(_testOutputHelper) ?? throw new InvalidOperationException($"Failed to get {nameof(HomeserverAbstraction)}");
    }

    [Fact]
    public async Task GetJoinedRoomsAsync() {
        var hs = await _hsAbstraction.GetNewHomeserver();
        //make 100 rooms
        var createRoomTasks = Enumerable.Range(0, 10).Select(_ => RoomAbstraction.GetTestRoom(hs)).ToList();
        await Task.WhenAll(createRoomTasks);

        var rooms = await hs.GetJoinedRooms();
        Assert.NotNull(rooms);
        Assert.NotEmpty(rooms);
        Assert.All(rooms, Assert.NotNull);
        // Assert.True(rooms.Count >= 10, "Not enough rooms were found");
        Assert.Equal(10, rooms.Count);
        await hs.Logout();
    }

    [Fact]
    public async Task GetMembersAsync() {
        Assert.True(StateEvent.KnownStateEventTypes is { Count: > 0 }, "StateEvent.KnownStateEventTypes is empty!");
        Assert.True(StateEvent.KnownStateEventTypesByName is { Count: > 0 }, "StateEvent.KnownStateEventTypesByName is empty!");

        var hs = await _hsAbstraction.GetConfiguredHomeserver();
        var room = await RoomAbstraction.GetTestRoom(hs);
        Assert.NotNull(room);
        var members = room.GetMembersEnumerableAsync();
        Assert.NotNull(members);
        var hitMembers = false;
        await foreach (var member in members) {
            Assert.NotNull(member);
            Assert.NotNull(member.StateKey);
            Assert.NotEmpty(member.StateKey);
            Assert.NotNull(member.Sender);
            Assert.NotEmpty(member.Sender);
            Assert.NotNull(member.RawContent);
            Assert.NotEmpty(member.RawContent);
            Assert.NotNull(member.TypedContent);
            Assert.IsType<RoomMemberEventContent>(member.TypedContent);
            var content = (RoomMemberEventContent)member.TypedContent;
            Assert.NotNull(content);
            Assert.NotNull(content.Membership);
            Assert.NotEmpty(content.Membership);
            hitMembers = true;
        }

        Assert.True(hitMembers, "No members were found in the room");

        await room.LeaveAsync();
    }

    [SkippableFact(typeof(MatrixException))]
    public async Task SendStateEventAsync() {
        var hs = await _hsAbstraction.GetConfiguredHomeserver();
        var room = await RoomAbstraction.GetTestRoom(hs);
        Assert.NotNull(room);

        await room.SendStateEventAsync("gay.rory.libmatrix.unit_tests", new UserProfileResponse() {
            DisplayName = "wee_woo",
            AvatarUrl = "no"
        });
        await room.SendStateEventAsync("gay.rory.libmatrix.unit_tests", "state_key_maybe", new UserProfileResponse() {
            DisplayName = "wee_woo",
            AvatarUrl = "yes"
        });

        await room.LeaveAsync();
    }

    [SkippableFact(typeof(MatrixException))]
    public async Task SendAndGetStateEventAsync() {
        await SendStateEventAsync();
        var hs = await _hsAbstraction.GetConfiguredHomeserver();
        var room = await RoomAbstraction.GetTestRoom(hs);
        Assert.NotNull(room);

        await room.SendStateEventAsync("gay.rory.libmatrix.unit_tests", new UserProfileResponse() {
            DisplayName = "wee_woo",
            AvatarUrl = "no"
        });
        await room.SendStateEventAsync("gay.rory.libmatrix.unit_tests", "state_key_maybe", new UserProfileResponse() {
            DisplayName = "wee_woo",
            AvatarUrl = "yes"
        });

        var state1 = await room.GetStateAsync<UserProfileResponse>("gay.rory.libmatrix.unit_tests");
        Assert.NotNull(state1);
        Assert.NotNull(state1.DisplayName);
        Assert.NotEmpty(state1.DisplayName);
        Assert.NotNull(state1.AvatarUrl);
        Assert.NotEmpty(state1.AvatarUrl);
        Assert.Equal("wee_woo", state1.DisplayName);
        Assert.Equal("no", state1.AvatarUrl);

        var state2 = await room.GetStateAsync<UserProfileResponse>("gay.rory.libmatrix.unit_tests", "state_key_maybe");
        Assert.NotNull(state2);
        Assert.NotNull(state2.DisplayName);
        Assert.NotEmpty(state2.DisplayName);
        Assert.NotNull(state2.AvatarUrl);
        Assert.NotEmpty(state2.AvatarUrl);
        Assert.Equal("wee_woo", state2.DisplayName);
        Assert.Equal("yes", state2.AvatarUrl);

        await room.LeaveAsync();
    }

    [Fact]
    public async Task DisbandAsync() {
        var hs = await _hsAbstraction.GetConfiguredHomeserver();
        var room = await RoomAbstraction.GetTestRoom(hs);
        Assert.NotNull(room);

        await room.PermanentlyBrickRoomAsync();

        await room.LeaveAsync();
    }

    [Fact]
    public async Task SendFileAsync() {
        var hs = await _hsAbstraction.GetConfiguredHomeserver();
        var room = await RoomAbstraction.GetTestRoom(hs);
        Assert.NotNull(room);

        var res = await room.SendFileAsync("test.txt", new MemoryStream(Encoding.UTF8.GetBytes("This test was written by Emma [it/its], member of the Rory& system." +
                                                                                               "\nIf you are reading this on matrix, it means the unit test for uploading a file works!")));
        Assert.NotNull(res);
        Assert.NotNull(res.EventId);

        await room.LeaveAsync();
    }

    [Fact]
    public async Task GetFullStateAsListAsync() {
        var hs = await _hsAbstraction.GetConfiguredHomeserver();
        var room = await RoomAbstraction.GetTestRoom(hs);
        Assert.NotNull(room);

        var state = await room.GetFullStateAsListAsync();
        Assert.NotNull(state);
        Assert.NotEmpty(state);
        Assert.All(state, Assert.NotNull);
        Assert.All(state, s => {
            Assert.NotNull(s.EventId);
            Assert.NotEmpty(s.EventId);
            Assert.NotNull(s.Sender);
            Assert.NotEmpty(s.Sender);
            Assert.NotNull(s.RawContent);
            Assert.NotNull(s.TypedContent);
        });

        await room.LeaveAsync();
    }

    [SkippableFact(typeof(LibMatrixException))]
    public async Task GetStateEventAsync() {
        var hs = await _hsAbstraction.GetConfiguredHomeserver();
        var room = await RoomAbstraction.GetTestRoom(hs);
        Assert.NotNull(room);

        var state = await room.GetStateEventAsync("m.room.name");
        Assert.NotNull(state);
        Assert.NotNull(state.EventId);
        Assert.NotEmpty(state.EventId);
        Assert.NotNull(state.Sender);
        Assert.NotEmpty(state.Sender);
        Assert.NotNull(state.RawContent);
        Assert.NotEmpty(state.RawContent);
        Assert.NotNull(state.TypedContent);

        await room.LeaveAsync();
    }

    [Fact]
    public async Task GetStateEventIdAsync() {
        var hs = await _hsAbstraction.GetNewHomeserver();
        var room = await RoomAbstraction.GetTestRoom(hs);
        Assert.NotNull(room);

        var state = await room.GetStateEventIdAsync("m.room.name");
        Assert.NotNull(state);
        Assert.NotEmpty(state);

        await room.LeaveAsync();
    }

    [SkippableFact(typeof(LibMatrixException))]
    public async Task GetStateEventOrNullAsync() {
        var hs = await _hsAbstraction.GetConfiguredHomeserver();
        var room = await RoomAbstraction.GetTestRoom(hs);
        Assert.NotNull(room);

        var state = await room.GetStateEventOrNullAsync("m.room.name");
        Assert.NotNull(state);
        Assert.NotNull(state.EventId);
        Assert.NotEmpty(state.EventId);
        Assert.NotNull(state.Sender);
        Assert.NotEmpty(state.Sender);
        Assert.NotNull(state.RawContent);
        Assert.NotEmpty(state.RawContent);
        Assert.NotNull(state.TypedContent);

        await room.LeaveAsync();
    }

    [Fact]
    public async Task GetMessagesAsync() {
        var hs = await _hsAbstraction.GetConfiguredHomeserver();
        var room = await RoomAbstraction.GetTestRoom(hs);
        Assert.NotNull(room);

        var messages = await room.GetMessagesAsync();
        Assert.NotNull(messages);
        Assert.NotNull(messages.Chunk);
        Assert.NotEmpty(messages.Chunk);
        Assert.All(messages.Chunk, Assert.NotNull);
        Assert.All(messages.Chunk, m => {
            Assert.NotNull(m.EventId);
            Assert.NotEmpty(m.EventId);
            Assert.NotNull(m.Sender);
            Assert.NotEmpty(m.Sender);
            Assert.NotNull(m.RawContent);
            Assert.NotNull(m.TypedContent);
        });

        await room.LeaveAsync();
        
        await File.WriteAllTextAsync("test.json", messages.ToJson());
    }

    [Fact]
    public async Task GetManyMessagesAsync() {
        var hs = await _hsAbstraction.GetConfiguredHomeserver();
        var room = await RoomAbstraction.GetTestRoom(hs);
        Assert.NotNull(room);

        var messages = room.GetManyMessagesAsync(chunkSize: 2);
        await foreach (var resp in messages) {
            Assert.NotNull(resp);
            Assert.NotNull(resp.Chunk);
            // Assert.NotEmpty(resp.Chunk);
            Assert.All(resp.Chunk, Assert.NotNull);
            Assert.All(resp.Chunk, m => {
                Assert.NotNull(m.EventId);
                Assert.NotEmpty(m.EventId);
                Assert.NotNull(m.Sender);
                Assert.NotEmpty(m.Sender);
                Assert.NotNull(m.RawContent);
                Assert.NotNull(m.TypedContent);
            });
        }

        await room.LeaveAsync();
    }
    
    [Fact]
    public async Task SendMessageEventAsync() {
        var hs = await _hsAbstraction.GetConfiguredHomeserver();
        var room = await RoomAbstraction.GetTestRoom(hs);
        Assert.NotNull(room);

        var res = await room.SendMessageEventAsync(new RoomMessageEventContent(body: "This test was written by Emma [it/its], member of the Rory& system." +
                                                                                     "\nIf you are reading this on matrix, it means the unit test for sending a message works!", messageType: "m.text"));
        Assert.NotNull(res);
        Assert.NotNull(res.EventId);

        await room.LeaveAsync();
    }
    
    [Fact]
    public async Task InviteUsersAsync() {
        var hs = await _hsAbstraction.GetConfiguredHomeserver();
        var room = await RoomAbstraction.GetTestRoom(hs);
        Assert.NotNull(room);

        var users = _hsAbstraction.GetNewHomeservers(32).ToBlockingEnumerable().ToList();
        Assert.NotNull(users);
        Assert.NotEmpty(users);
        Assert.All(users, Assert.NotNull);
        Assert.All(users, u => {
            Assert.NotNull(u);
            Assert.NotNull(u.UserId);
            Assert.NotEmpty(u.UserId);
        });
        
        await room.InviteUsersAsync(users.Select(u => u.UserId));
        var members = await room.GetMembersListAsync();
        Assert.NotNull(members);
        Assert.NotEmpty(members);
        Assert.All(members, Assert.NotNull);
        Assert.All(members, m => {
            Assert.NotNull(m);
            Assert.NotNull(m.StateKey);
            Assert.NotEmpty(m.StateKey);
        });
        Assert.All(users, u => Assert.Contains(u.UserId, members.Select(m => m.StateKey)));
        
        await room.LeaveAsync();
    }
}