using ArcaneLibs.Extensions;
using LibMatrix.EventTypes.Spec.State;
using LibMatrix.EventTypes.Spec.State.RoomInfo;
using LibMatrix.Homeservers;
using LibMatrix.Tests.Abstractions;
using LibMatrix.Tests.Fixtures;
using Xunit.Abstractions;
using Xunit.Microsoft.DependencyInjection.Abstracts;

namespace LibMatrix.Tests.Tests;

public class RoomMembershipTests : TestBed<TestFixture> {
    private readonly HomeserverAbstraction _hsAbstraction;

    public RoomMembershipTests(ITestOutputHelper testOutputHelper, TestFixture fixture) : base(testOutputHelper, fixture) {
        _hsAbstraction = _fixture.GetService<HomeserverAbstraction>(_testOutputHelper) ?? throw new InvalidOperationException($"Failed to get {nameof(HomeserverAbstraction)}");
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

    [Fact]
    public async Task JoinAsync() {
        var hs = await _hsAbstraction.GetConfiguredHomeserver(_testOutputHelper);
        var hs2 = await _hsAbstraction.GetNewHomeserver();
        var room = await RoomAbstraction.GetTestRoom(hs);
        Assert.NotNull(room);
        await room.SendStateEventAsync(RoomJoinRulesEventContent.EventId, new RoomJoinRulesEventContent() {
            JoinRule = RoomJoinRulesEventContent.JoinRules.Public
        });
        // var id = await room.JoinAsync();
        var id = await hs2.GetRoom(room.RoomId).JoinAsync();
        Assert.NotNull(id);
        Assert.NotNull(id.RoomId);
        Assert.NotEmpty(id.RoomId);
        
        await room.LeaveAsync();
    }

    [Fact]
    public async Task ForgetAsync() {
        var hs = await _hsAbstraction.GetConfiguredHomeserver();
        var room = await RoomAbstraction.GetTestRoom(hs);
        Assert.NotNull(room);
        await room.ForgetAsync();
    }

    [Fact]
    public async Task LeaveAsync() {
        var hs = await _hsAbstraction.GetConfiguredHomeserver();
        var room = await RoomAbstraction.GetTestRoom(hs);
        Assert.NotNull(room);
        await room.LeaveAsync();
    }

    [Fact]
    public async Task KickAsync() {
        var hs = await _hsAbstraction.GetConfiguredHomeserver();
        var hs2 = await _hsAbstraction.GetNewHomeserver();
        var room = await RoomAbstraction.GetTestRoom(hs);
        Assert.NotNull(room);
        await room.InviteUserAsync(hs2.UserId, "Unit test!");
        await hs2.GetRoom(room.RoomId).JoinAsync();
        await room.KickAsync(hs2.UserId, "test");
        var banState = await room.GetStateAsync<RoomMemberEventContent>("m.room.member", hs2.UserId);
        Assert.NotNull(banState);
        Assert.Equal("leave", banState.Membership);
        Assert.Equal("test", banState.Reason);
        
        await room.LeaveAsync();
    }

    [Fact]
    public async Task BanAsync() {
        var hs = await _hsAbstraction.GetConfiguredHomeserver();
        var hs2 = await _hsAbstraction.GetNewHomeserver();
        var room = await RoomAbstraction.GetTestRoom(hs);
        Assert.NotNull(room);
        await room.BanAsync(hs2.UserId, "test");
        var banState = await room.GetStateAsync<RoomMemberEventContent>("m.room.member", hs2.UserId);
        Assert.NotNull(banState);
        Assert.Equal("ban", banState.Membership);
        
        await room.LeaveAsync();
    }

    [Fact]
    public async Task UnbanAsync() {
        var hs = await _hsAbstraction.GetConfiguredHomeserver();
        var hs2 = await _hsAbstraction.GetNewHomeserver();
        var room = await RoomAbstraction.GetTestRoom(hs);
        Assert.NotNull(room);
        await room.BanAsync(hs2.UserId, "test");
        var banState = await room.GetStateAsync<RoomMemberEventContent>("m.room.member", hs2.UserId);
        Assert.NotNull(banState);
        Assert.Equal("ban", banState.Membership);
        await room.UnbanAsync(hs2.UserId, "testing");
        
        var unbanState = await room.GetStateAsync<RoomMemberEventContent>("m.room.member", hs2.UserId);
        Assert.NotNull(unbanState);
        Assert.Equal("leave", unbanState.Membership);
        Assert.Equal("testing", unbanState.Reason);
        
        await room.LeaveAsync();
    }

    [Fact]
    public async Task InviteAndJoinAsync() {
        int count = 5;

        var hs = await _hsAbstraction.GetConfiguredHomeserver();
        var room = await RoomAbstraction.GetTestRoom(hs);
        var otherUsers = _hsAbstraction.GetNewHomeservers(count);
        Assert.NotNull(room);

        // var expectedCount = 1;

        // var tasks = new List<Task>();
        // await foreach (var otherUser in otherUsers)
        // tasks.AddRange([
        // room.InviteUserAsync(otherUser.UserId),
        // otherUser.GetRoom(room.RoomId).JoinAsync()
        // ]);

        Dictionary<AuthenticatedHomeserverGeneric, Task> tasks = new();
        await foreach (var otherUser in otherUsers) {
            _testOutputHelper.WriteLine($"Inviting {otherUser.UserId} to {room.RoomId}");
            tasks.Add(otherUser, room.InviteUserAsync(otherUser.UserId, "Unit test!"));
        }

        await foreach (var otherUser in tasks.ToAsyncEnumerable()) {
            _testOutputHelper.WriteLine($"Joining {otherUser.UserId} to {room.RoomId}");
            await otherUser.GetRoom(room.RoomId).JoinAsync(reason: "Unit test!");
        }

        var states = await room.GetMembersListAsync(false);
        Assert.Equal(count + 1, states.Count);
        
        await room.LeaveAsync();
        await foreach (var authenticatedHomeserverGeneric in otherUsers)
        {
            await authenticatedHomeserverGeneric.GetRoom(room.RoomId).LeaveAsync();
        }
    }
}