using LibMatrix.Services;
using LibMatrix.Tests.Fixtures;
using Xunit.Abstractions;
using Xunit.Microsoft.DependencyInjection.Abstracts;

namespace LibMatrix.Tests.Tests;

public class RemoteHomeserverTests : TestBed<TestFixture> {
    private readonly TestFixture _fixture;
    private readonly HomeserverResolverService _resolver;
    private readonly Config _config;
    private readonly HomeserverProviderService _provider;

    public RemoteHomeserverTests(ITestOutputHelper testOutputHelper, TestFixture fixture) : base(testOutputHelper, fixture) {
        _fixture = fixture;
        _resolver = _fixture.GetService<HomeserverResolverService>(_testOutputHelper) ?? throw new InvalidOperationException($"Failed to get {nameof(HomeserverResolverService)}");
        _config = _fixture.GetService<Config>(_testOutputHelper) ?? throw new InvalidOperationException($"Failed to get {nameof(Config)}");
        _provider = _fixture.GetService<HomeserverProviderService>(_testOutputHelper) ?? throw new InvalidOperationException($"Failed to get {nameof(HomeserverProviderService)}");
    }

    // [Fact]
    // public async Task ResolveMedia() {
    //     var hs = await _provider.GetRemoteHomeserver("matrix.org");
    //     var media = hs.ResolveMediaUri("mxc://matrix.org/eqwrRZRoPpNbcMeUwyXAuVRo");
    //     
    //     Assert.Equal("https://matrix-client.matrix.org/_matrix/media/v3/download/matrix.org/eqwrRZRoPpNbcMeUwyXAuVRo", media);
    // }

    [Fact]
    public async Task ResolveRoomAliasAsync() {
        // var hs = await _provider.GetRemoteHomeserver("matrix.org");
        // var alias = await hs.ResolveRoomAliasAsync("#matrix:matrix.org");
        // Assert.Equal("!OGEhHVWSdvArJzumhm:matrix.org", alias.RoomId);
        var tasks = _config.ExpectedAliasMappings.Select(async mapping => {
            var hs = await _provider.GetRemoteHomeserver("matrix.org");
            var alias = await hs.ResolveRoomAliasAsync(mapping.Key);
            Assert.Equal(mapping.Value, alias.RoomId);
            return alias;
        }).ToList();
        await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task GetClientVersionsAsync() {
        var hs = await _provider.GetRemoteHomeserver(_config.TestHomeserver);
        var versions = await hs.GetClientVersionsAsync();
        Assert.NotNull(versions);
    }

    [Fact]
    public async Task GetProfileAsync() {
        var hs = await _provider.GetRemoteHomeserver("matrix.org");
        var profile = await hs.GetProfileAsync("@alice-is-:matrix.org");
        Assert.NotNull(profile);
    }
}