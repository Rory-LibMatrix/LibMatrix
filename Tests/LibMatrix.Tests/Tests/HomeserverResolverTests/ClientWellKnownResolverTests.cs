using LibMatrix.Services;
using LibMatrix.Services.WellKnownResolver.WellKnownResolvers;
using LibMatrix.Tests.Fixtures;
using Xunit.Abstractions;
using Xunit.Microsoft.DependencyInjection.Abstracts;

namespace LibMatrix.Tests.Tests.HomeserverResolverTests;

public class ClientWellKnownResolverTests : TestBed<TestFixture> {
    private readonly Config _config;
    private readonly ClientWellKnownResolver _resolver;

    public ClientWellKnownResolverTests(ITestOutputHelper testOutputHelper, TestFixture fixture) : base(testOutputHelper, fixture) {
        _config = _fixture.GetService<Config>(_testOutputHelper) ?? throw new InvalidOperationException($"Failed to get {nameof(Config)}");
        _resolver = _fixture.GetService<ClientWellKnownResolver>(_testOutputHelper) ?? throw new InvalidOperationException($"Failed to get {nameof(HomeserverResolverService)}");
    }

    [Fact]
    public async Task ResolveServerClient() {
        var tasks = _config.ExpectedHomeserverClientMappings.Select(async mapping => {
            var server = await _resolver.TryResolveWellKnown(mapping.Key);
            Assert.Equal(mapping.Value, server.Content.Homeserver.BaseUrl);
            return server;
        }).ToList();
        await Task.WhenAll(tasks);
    }

    private async Task AssertClientWellKnown(string homeserver, string expected) {
        var server = await _resolver.TryResolveWellKnown(homeserver);
        Assert.Equal(expected, server.Content.Homeserver.BaseUrl);
    }

    [Fact]
    public Task ResolveMatrixOrg() => AssertClientWellKnown("matrix.org", "https://matrix-client.matrix.org");

    [Fact]
    public Task ResolveRoryGay() => AssertClientWellKnown("rory.gay", "https://matrix.rory.gay");
    
    [Fact]
    public Task ResolveTransfemDev() => AssertClientWellKnown("transfem.dev", "https://matrix.transfem.dev/");
}