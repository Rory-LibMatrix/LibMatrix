using LibMatrix.Services;
using LibMatrix.Tests.DataTests;
using LibMatrix.Tests.Fixtures;
using Xunit.Abstractions;
using Xunit.Microsoft.DependencyInjection.Abstracts;

namespace LibMatrix.Tests.Tests;

public class AuthTests : TestBed<TestFixture> {
    private readonly TestFixture _fixture;
    private readonly HomeserverResolverService _resolver;
    private readonly Config _config;
    private readonly HomeserverProviderService _provider;

    public AuthTests(ITestOutputHelper testOutputHelper, TestFixture fixture) : base(testOutputHelper, fixture) {
        _fixture = fixture;
        _resolver = _fixture.GetService<HomeserverResolverService>(_testOutputHelper) ?? throw new InvalidOperationException($"Failed to get {nameof(HomeserverResolverService)}");
        _config = _fixture.GetService<Config>(_testOutputHelper) ?? throw new InvalidOperationException($"Failed to get {nameof(Config)}");
        _provider = _fixture.GetService<HomeserverProviderService>(_testOutputHelper) ?? throw new InvalidOperationException($"Failed to get {nameof(HomeserverProviderService)}");
    }

    [Fact]
    public async Task LoginWithPassword() {
        Assert.False(string.IsNullOrWhiteSpace(_config.TestHomeserver), $"{nameof(_config.TestHomeserver)} must be set in appsettings!");
        Assert.False(string.IsNullOrWhiteSpace(_config.TestUsername), $"{nameof(_config.TestUsername)} must be set in appsettings!");
        Assert.False(string.IsNullOrWhiteSpace(_config.TestPassword), $"{nameof(_config.TestPassword)} must be set in appsettings!");

        // var server = await _resolver.ResolveHomeserverFromWellKnown(_config.TestHomeserver!);
        var rhs = await _provider.GetRemoteHomeserver(_config.TestHomeserver);
        var username = Guid.NewGuid().ToString();
        var password = Guid.NewGuid().ToString();
        
        var reg = await rhs.RegisterAsync(username, password, "Unit tests!");
        
        var login = await _provider.Login(_config.TestHomeserver!, username, password);
        Assert.NotNull(login);
        var hs = await _provider.GetAuthenticatedWithToken(_config.TestHomeserver!, login.AccessToken);
        Assert.NotNull(hs);
        await hs.Logout();
    }

    [Fact]
    public async Task LoginWithToken() {
        Assert.False(string.IsNullOrWhiteSpace(_config.TestHomeserver), $"{nameof(_config.TestHomeserver)} must be set in appsettings!");
        Assert.False(string.IsNullOrWhiteSpace(_config.TestUsername), $"{nameof(_config.TestUsername)} must be set in appsettings!");
        Assert.False(string.IsNullOrWhiteSpace(_config.TestPassword), $"{nameof(_config.TestPassword)} must be set in appsettings!");

        // var server = await _resolver.ResolveHomeserverFromWellKnown(_config.TestHomeserver!);
        var rhs = await _provider.GetRemoteHomeserver(_config.TestHomeserver);
        var username = Guid.NewGuid().ToString();
        var password = Guid.NewGuid().ToString();
        
        var reg = await rhs.RegisterAsync(username, password, "Unit tests!");
        
        var hs = await _provider.GetAuthenticatedWithToken(_config.TestHomeserver!, reg.AccessToken);
        Assert.NotNull(hs);
        Assert.NotNull(hs.WhoAmI);
        hs.WhoAmI.VerifyRequiredFields();
        Assert.NotNull(hs.UserId);
        Assert.NotNull(hs.AccessToken);
        await hs.Logout();
    }

    [Fact]
    public async Task RegisterAsync() {
        var rhs = await _provider.GetRemoteHomeserver("matrixunittests.rory.gay");
        var reg = await rhs.RegisterAsync(Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), "Unit tests!");
        Assert.NotNull(reg);
        Assert.NotNull(reg.AccessToken);
        Assert.NotNull(reg.DeviceId);
        Assert.NotNull(reg.UserId);
        var hs = await _provider.GetAuthenticatedWithToken(reg.Homeserver, reg.AccessToken);
        Assert.NotNull(hs);
        Assert.NotNull(hs.WhoAmI);
        hs.WhoAmI.VerifyRequiredFields();
        Assert.NotNull(hs.UserId);
        Assert.NotNull(hs.AccessToken);
        await hs.Logout();
    }
}