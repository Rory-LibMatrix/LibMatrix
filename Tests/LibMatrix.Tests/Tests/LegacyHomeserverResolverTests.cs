using LibMatrix.Services;
using LibMatrix.Tests.Fixtures;
using Xunit.Abstractions;
using Xunit.Microsoft.DependencyInjection.Abstracts;

namespace LibMatrix.Tests.Tests;

public class LegacyHomeserverResolverTests : TestBed<TestFixture> {
    private readonly Config _config;
    private readonly HomeserverResolverService _resolver;

    public LegacyHomeserverResolverTests(ITestOutputHelper testOutputHelper, TestFixture fixture) : base(testOutputHelper, fixture) {
        _config = _fixture.GetService<Config>(_testOutputHelper) ?? throw new InvalidOperationException($"Failed to get {nameof(Config)}");
        _resolver = _fixture.GetService<HomeserverResolverService>(_testOutputHelper) ?? throw new InvalidOperationException($"Failed to get {nameof(HomeserverResolverService)}");
    }

    [Fact]
    public async Task ResolveServerClient() {
        var tasks = _config.ExpectedHomeserverClientMappings.Select(async mapping => {
            var server = await _resolver.ResolveHomeserverFromWellKnown(mapping.Key);
            Assert.Equal(mapping.Value, server.Client);
            return server;
        }).ToList();
        await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task ResolveServerServer() {
        var tasks = _config.ExpectedHomeserverFederationMappings.Select(async mapping => {
            var server = await _resolver.ResolveHomeserverFromWellKnown(mapping.Key);
            Assert.Equal(mapping.Value, server.Server);
            return server;
        }).ToList();
        await Task.WhenAll(tasks);
    }
}