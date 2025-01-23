using LibMatrix.Tests.Abstractions;
using LibMatrix.Tests.Fixtures;
using Xunit.Abstractions;
using Xunit.Microsoft.DependencyInjection.Abstracts;

namespace LibMatrix.Tests.Tests;

public class RoomEventTests : TestBed<TestFixture> {
    private readonly HomeserverAbstraction _hsAbstraction;

    public RoomEventTests(ITestOutputHelper testOutputHelper, TestFixture fixture) : base(testOutputHelper, fixture) {
        _hsAbstraction = _fixture.GetService<HomeserverAbstraction>(_testOutputHelper) ?? throw new InvalidOperationException($"Failed to get {nameof(HomeserverAbstraction)}");
    }

    [Fact]
    public async Task GetNameAsync() {
        var hs = await _hsAbstraction.GetConfiguredHomeserver();
        
        var room = await RoomAbstraction.GetTestRoom(hs);
        Assert.NotNull(room);
        var name = await room.GetNameAsync();
        Assert.NotNull(name);
        Assert.NotEmpty(name);
    }

    [SkippableFact(typeof(MatrixException))]
    public async Task GetTopicAsync() {
        var hs = await _hsAbstraction.GetConfiguredHomeserver();
        var room = await RoomAbstraction.GetTestRoom(hs);
        Assert.NotNull(room);
        var topic = await room.GetTopicAsync();
        Assert.NotNull(topic);
        Assert.NotNull(topic.Topic);
        Assert.NotEmpty(topic.Topic);
    }

    [SkippableFact(typeof(MatrixException))]
    public async Task GetCanonicalAliasAsync() {
        var hs = await _hsAbstraction.GetConfiguredHomeserver();
        var room = await RoomAbstraction.GetTestRoom(hs);
        Assert.NotNull(room);
        var alias = await room.GetCanonicalAliasAsync();
        Assert.NotNull(alias);
        Assert.NotNull(alias.Alias);
        Assert.NotEmpty(alias.Alias);
    }

    [SkippableFact(typeof(MatrixException))]
    public async Task GetAvatarUrlAsync() {
        var hs = await _hsAbstraction.GetConfiguredHomeserver();
        var room = await RoomAbstraction.GetTestRoom(hs);
        Assert.NotNull(room);
        var url = await room.GetAvatarUrlAsync();
        Assert.NotNull(url);
        Assert.NotNull(url.Url);
        Assert.NotEmpty(url.Url);

        await room.LeaveAsync();
    }

    [Fact]
    public async Task GetJoinRuleAsync() {
        var hs = await _hsAbstraction.GetConfiguredHomeserver();
        var room = await RoomAbstraction.GetTestRoom(hs);
        Assert.NotNull(room);
        var rule = await room.GetJoinRuleAsync();
        Assert.NotNull(rule);
        Assert.NotNull(rule.JoinRuleValue);
        Assert.NotEmpty(rule.JoinRuleValue);
        
        await room.LeaveAsync();
    }

    [Fact]
    public async Task GetHistoryVisibilityAsync() {
        var hs = await _hsAbstraction.GetConfiguredHomeserver();
        var room = await RoomAbstraction.GetTestRoom(hs);
        Assert.NotNull(room);
        var visibility = await room.GetHistoryVisibilityAsync();
        Assert.NotNull(visibility);
        Assert.NotNull(visibility.HistoryVisibility);
        Assert.NotEmpty(visibility.HistoryVisibility);
        
        await room.LeaveAsync();
    }

    [Fact]
    public async Task GetGuestAccessAsync() {
        var hs = await _hsAbstraction.GetConfiguredHomeserver();
        var room = await RoomAbstraction.GetTestRoom(hs);
        Assert.NotNull(room);
        try {
            var access = await room.GetGuestAccessAsync();
            Assert.NotNull(access);
            Assert.NotNull(access.GuestAccess);
            Assert.NotEmpty(access.GuestAccess);
        }
        catch (Exception e) {
            if (e is not MatrixException exception) throw;
            Assert.Equal("M_NOT_FOUND", exception.ErrorCode);
        }
        
        await room.LeaveAsync();
    }

    [Fact]
    public async Task GetCreateEventAsync() {
        var hs = await _hsAbstraction.GetConfiguredHomeserver();
        var room = await RoomAbstraction.GetTestRoom(hs);
        Assert.NotNull(room);
        var create = await room.GetCreateEventAsync();
        Assert.NotNull(create);
        Assert.NotNull(create.Creator);
        Assert.NotEmpty(create.RoomVersion!);
        
        await room.LeaveAsync();
    }

    [Fact]
    public async Task GetRoomType() {
        var hs = await _hsAbstraction.GetConfiguredHomeserver();
        var room = await RoomAbstraction.GetTestRoom(hs);
        Assert.NotNull(room);
        await room.GetRoomType();
        
        await room.LeaveAsync();
    }

    [Fact]
    public async Task GetPowerLevelsAsync() {
        var hs = await _hsAbstraction.GetConfiguredHomeserver();
        var room = await RoomAbstraction.GetTestRoom(hs);
        Assert.NotNull(room);
        var power = await room.GetPowerLevelsAsync();
        Assert.NotNull(power);
        Assert.NotNull(power.Ban);
        Assert.NotNull(power.Kick);
        Assert.NotNull(power.Invite);
        Assert.NotNull(power.Redact);
        Assert.NotNull(power.StateDefault);
        Assert.NotNull(power.EventsDefault);
        Assert.NotNull(power.UsersDefault);
        Assert.NotNull(power.Users);
        // Assert.NotNull(power.Events);
        
        await room.LeaveAsync();
    }
}