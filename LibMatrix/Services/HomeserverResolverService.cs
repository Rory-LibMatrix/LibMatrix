using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ArcaneLibs.Collections;
using ArcaneLibs.Extensions;
using LibMatrix.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LibMatrix.Services;

public class HomeserverResolverService {
    private readonly MatrixHttpClient _httpClient = new() {
        // Timeout = TimeSpan.FromSeconds(60) // TODO: Re-implement this
    };

    private static readonly SemaphoreCache<WellKnownUris> WellKnownCache = new();

    private readonly ILogger<HomeserverResolverService> _logger;

    public HomeserverResolverService(ILogger<HomeserverResolverService> logger) {
        _logger = logger;
        if (logger is NullLogger<HomeserverResolverService>) {
            var stackFrame = new StackTrace(true).GetFrame(1);
            Console.WriteLine(
                $"WARN | Null logger provided to HomeserverResolverService!\n{stackFrame.GetMethod().DeclaringType} at {stackFrame.GetFileName()}:{stackFrame.GetFileLineNumber()}");
        }
    }

    // private static SemaphoreSlim _wellKnownSemaphore = new(1, 1);

    public async Task<WellKnownUris> ResolveHomeserverFromWellKnown(string homeserver, bool enableClient = true, bool enableServer = true) {
        ArgumentNullException.ThrowIfNull(homeserver);

        return await WellKnownCache.GetOrAdd(homeserver, async () => {
            // await _wellKnownSemaphore.WaitAsync();
            _logger.LogTrace($"Resolving homeserver well-knowns: {homeserver}");
            var client = enableClient ? _tryResolveClientEndpoint(homeserver) : null;
            var server = enableServer ? _tryResolveServerEndpoint(homeserver) : null;

            var res = new WellKnownUris();

            // try {
            if (client != null)
                res.Client = (await client)?.TrimEnd('/') ?? throw new Exception($"Could not resolve client URL for {homeserver}.");
            // }
            // catch (Exception e) {
            // _logger.LogError(e, "Error resolving client well-known for {hs}", homeserver);
            // }

            // try {
            if (server != null)
                res.Server = (await server)?.TrimEnd('/') ?? throw new Exception($"Could not resolve server URL for {homeserver}.");
            // }
            // catch (Exception e) {
            // _logger.LogError(e, "Error resolving server well-known for {hs}", homeserver);
            // }

            _logger.LogInformation("Resolved well-knowns for {hs}: {json}", homeserver, res.ToJson(indent: false));
            // _wellKnownSemaphore.Release();
            return res;
        });
    }

    // private async Task<WellKnownUris> InternalResolveHomeserverFromWellKnown(string homeserver) {

    // }

    private async Task<string?> _tryResolveClientEndpoint(string homeserver) {
        ArgumentNullException.ThrowIfNull(homeserver);
        _logger.LogTrace("Resolving client well-known: {homeserver}", homeserver);
        ClientWellKnown? clientWellKnown = null;
        // check if homeserver has a client well-known
        if (homeserver.StartsWith("https://")) {
            clientWellKnown = await _httpClient.TryGetFromJsonAsync<ClientWellKnown>($"{homeserver}/.well-known/matrix/client");
        }
        else if (homeserver.StartsWith("http://")) {
            clientWellKnown = await _httpClient.TryGetFromJsonAsync<ClientWellKnown>($"{homeserver}/.well-known/matrix/client");
        }
        else {
            clientWellKnown ??= await _httpClient.TryGetFromJsonAsync<ClientWellKnown>($"https://{homeserver}/.well-known/matrix/client");
            clientWellKnown ??= await _httpClient.TryGetFromJsonAsync<ClientWellKnown>($"http://{homeserver}/.well-known/matrix/client");

            if (clientWellKnown is null) {
                if (await _httpClient.CheckSuccessStatus($"https://{homeserver}/_matrix/client/versions"))
                    return $"https://{homeserver}";
                if (await _httpClient.CheckSuccessStatus($"http://{homeserver}/_matrix/client/versions"))
                    return $"http://{homeserver}";
            }
        }

        if (!string.IsNullOrWhiteSpace(clientWellKnown?.Homeserver.BaseUrl))
            return clientWellKnown.Homeserver.BaseUrl;

        _logger.LogInformation("No client well-known for {server}...", homeserver);
        return null;
    }

    private async Task<string?> _tryResolveServerEndpoint(string homeserver) {
        // TODO: implement SRV delegation via DoH: https://developers.google.com/speed/public-dns/docs/doh/json
        ArgumentNullException.ThrowIfNull(homeserver);
        _logger.LogTrace($"Resolving server well-known: {homeserver}");
        ServerWellKnown? serverWellKnown = null;
        // check if homeserver has a server well-known
        if (homeserver.StartsWith("https://")) {
            serverWellKnown = await _httpClient.TryGetFromJsonAsync<ServerWellKnown>($"{homeserver}/.well-known/matrix/server");
        }
        else if (homeserver.StartsWith("http://")) {
            serverWellKnown = await _httpClient.TryGetFromJsonAsync<ServerWellKnown>($"{homeserver}/.well-known/matrix/server");
        }
        else {
            serverWellKnown ??= await _httpClient.TryGetFromJsonAsync<ServerWellKnown>($"https://{homeserver}/.well-known/matrix/server");
            serverWellKnown ??= await _httpClient.TryGetFromJsonAsync<ServerWellKnown>($"http://{homeserver}/.well-known/matrix/server");
        }

        _logger.LogInformation("Server well-known for {hs}: {json}", homeserver, serverWellKnown?.ToJson() ?? "null");

        if (!string.IsNullOrWhiteSpace(serverWellKnown?.Homeserver)) {
            var resolved = serverWellKnown.Homeserver;
            if (resolved.StartsWith("https://") || resolved.StartsWith("http://"))
                return resolved;
            if (await _httpClient.CheckSuccessStatus($"https://{resolved}/_matrix/federation/v1/version"))
                return $"https://{resolved}";
            if (await _httpClient.CheckSuccessStatus($"http://{resolved}/_matrix/federation/v1/version"))
                return $"http://{resolved}";
            _logger.LogWarning("Server well-known points to invalid server: {resolved}", resolved);
        }

        // fallback: most servers host C2S and S2S on the same domain
        var clientUrl = await _tryResolveClientEndpoint(homeserver);
        if (clientUrl is not null && await _httpClient.CheckSuccessStatus($"{clientUrl}/_matrix/federation/v1/version"))
            return clientUrl;

        _logger.LogInformation("No server well-known for {server}...", homeserver);
        return null;
    }

    public async Task<string?> ResolveMediaUri(string homeserver, string mxc) {
        if (homeserver is null) throw new ArgumentNullException(nameof(homeserver));
        if (mxc is null) throw new ArgumentNullException(nameof(mxc));
        if (!mxc.StartsWith("mxc://")) throw new InvalidDataException("mxc must start with mxc://");
        homeserver = (await ResolveHomeserverFromWellKnown(homeserver)).Client;
        return mxc.Replace("mxc://", $"{homeserver}/_matrix/media/v3/download/");
    }

    public class WellKnownUris {
        public string? Client { get; set; }
        public string? Server { get; set; }
    }

    public class ClientWellKnown {
        [JsonPropertyName("m.homeserver")]
        public WellKnownHomeserver Homeserver { get; set; }

        public class WellKnownHomeserver {
            [JsonPropertyName("base_url")]
            public string BaseUrl { get; set; }
        }
    }

    public class ServerWellKnown {
        [JsonPropertyName("m.server")]
        public string Homeserver { get; set; }
    }
}