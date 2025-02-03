using LibMatrix.EventTypes.Spec.State.RoomInfo;
using LibMatrix.Tests.Abstractions;
using LibMatrix.Tests.Fixtures;
using Xunit.Abstractions;
using Xunit.Microsoft.DependencyInjection.Abstracts;

namespace LibMatrix.Tests.Tests.BasicRoomEventTests;

public class RoomNameTests : TestBed<TestFixture> {
    private readonly HomeserverAbstraction _hsAbstraction;

    public RoomNameTests(ITestOutputHelper testOutputHelper, TestFixture fixture) : base(testOutputHelper, fixture) {
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

        await room.LeaveAsync();
    }

    [Fact]
    public async Task SetNameAsync() {
        var hs = await _hsAbstraction.GetConfiguredHomeserver();

        var room = await RoomAbstraction.GetTestRoom(hs);
        Assert.NotNull(room);
        var name = Guid.NewGuid().ToString();
        await room.SendStateEventAsync(RoomNameEventContent.EventId, new RoomNameEventContent { Name = name });
        var newName = await room.GetNameAsync();
        Assert.Equal(name, newName);
        
        await room.LeaveAsync();
    }
}