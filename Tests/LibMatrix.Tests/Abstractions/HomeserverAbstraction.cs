using ArcaneLibs.Extensions;
using LibMatrix.Homeservers;
using LibMatrix.Responses;
using LibMatrix.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace LibMatrix.Tests.Abstractions;

public class HomeserverAbstraction(HomeserverProviderService _hsProvider, Config _config, ILogger<HomeserverAbstraction> _logger) {
    // private static readonly HomeserverResolverService _hsResolver = new HomeserverResolverService(NullLogger<HomeserverResolverService>.Instance);
    // private static readonly HomeserverProviderService _hsProvider = new HomeserverProviderService(NullLogger<HomeserverProviderService>.Instance, _hsResolver);
    
    private static AuthenticatedHomeserverGeneric? ConfiguredHomeserver { get; set; }
    private static readonly SemaphoreSlim _lock = new(1, 1);
    
    public async Task<AuthenticatedHomeserverGeneric> GetConfiguredHomeserver(ITestOutputHelper? testOutputHelper = null) {
        Assert.False(string.IsNullOrWhiteSpace(_config.TestHomeserver));
        Assert.False(string.IsNullOrWhiteSpace(_config.TestUsername));
        Assert.False(string.IsNullOrWhiteSpace(_config.TestPassword));
        
        _logger.LogDebug("Using homeserver '{0}' with login '{1}' '{2}", _config.TestHomeserver, _config.TestUsername, _config.TestPassword);
        testOutputHelper?.WriteLine($"Using homeserver '{_config.TestHomeserver}' with login '{_config.TestUsername}' '{_config.TestPassword}'");

        await _lock.WaitAsync();
        if (ConfiguredHomeserver is not null) {
            _lock.Release();
            return ConfiguredHomeserver;
        }

        var rhs = await _hsProvider.GetRemoteHomeserver(_config.TestHomeserver);
        
        LoginResponse reg;
        try {
            reg = await rhs.LoginAsync(_config.TestUsername, _config.TestPassword);
        }
        catch (MatrixException e) {
            if (e.ErrorCode == "M_FORBIDDEN") {
                await rhs.RegisterAsync(Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), "Unit tests!");
                reg = await rhs.RegisterAsync(_config.TestUsername, _config.TestPassword, "Unit tests!");
            }
            else throw new Exception("Failed to register", e);
        }

        var hs = await _hsProvider.GetAuthenticatedWithToken(reg.Homeserver, reg.AccessToken);
        ConfiguredHomeserver = hs;
        _lock.Release();

        return hs;
    }

    public async Task<AuthenticatedHomeserverGeneric> GetNewHomeserver() {
        Assert.False(string.IsNullOrWhiteSpace(_config.TestHomeserver));
        var username = Guid.NewGuid().ToString();
        var password = Guid.NewGuid().ToString();
        
        _logger.LogDebug("Creating new homeserver '{0}' with login '{1}' '{2}'", _config.TestHomeserver, username, password);
        
        var rhs = await _hsProvider.GetRemoteHomeserver(_config.TestHomeserver);
        var reg = await rhs.RegisterAsync(username, password, "Unit tests!");
        var hs = await _hsProvider.GetAuthenticatedWithToken(reg.Homeserver, reg.AccessToken);
        
        return hs;
    }

    public async IAsyncEnumerable<AuthenticatedHomeserverGeneric> GetNewHomeservers(int count = 1) {
        var createRandomUserTasks = Enumerable
            .Range(0, count)
            .Select(_ => GetNewHomeserver()).ToAsyncEnumerable();
        await foreach (var hs in createRandomUserTasks) yield return hs;
    }

    public async Task<(string username, string password, string token)> GetKnownCredentials() {
        Assert.False(string.IsNullOrWhiteSpace(_config.TestHomeserver));
        var rhs = await _hsProvider.GetRemoteHomeserver(_config.TestHomeserver);
        
        var username = _config.TestUsername;
        var password = _config.TestPassword;
        var reg = await rhs.RegisterAsync(username, password, "Unit tests!");
        return ("", "", "");
    }
}