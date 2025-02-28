using System.Text.Json.Serialization;
using ArcaneLibs.Collections;
using LibMatrix.Extensions;
using Microsoft.Extensions.Logging;

namespace LibMatrix.Services.WellKnownResolvers;

public class ClientWellKnownResolver(ILogger<ClientWellKnownResolver> logger) {
    private static readonly SemaphoreCache<WellKnownResolutionResult> ClientWellKnownCache = new() {
        StoreNulls = false
    };
    private static readonly MatrixHttpClient HttpClient = new();

    public Task<WellKnownResolutionResult> TryResolveClientWellKnown(string homeserver) {
        return ClientWellKnownCache.TryGetOrAdd(homeserver, async () => {
            logger.LogTrace($"Resolving client well-known: {homeserver}");
            if ((await TryGetClientWellKnownFromHttps(homeserver)) is { } clientWellKnown)
                return new() {
                    Source = WellKnownResolverService.WellKnownSource.Https,
                    WellKnown = clientWellKnown
                };

            return default!;
        });
    }

    private async Task<ClientWellKnown?> TryGetClientWellKnownFromHttps(string homeserver) {
        try {
            return await HttpClient.TryGetFromJsonAsync<ClientWellKnown>($"https://{homeserver}/.well-known/matrix/client");
        }
        catch {
            return null;
        }
    }



    public class ClientWellKnown {
        [JsonPropertyName("m.homeserver")]
        public required WellKnownHomeserver Homeserver { get; set; }

        public class WellKnownHomeserver {
            [JsonPropertyName("base_url")]
            public required string BaseUrl { get; set; }
        }
    }
    
    public struct WellKnownResolutionResult {
        public WellKnownResolverService.WellKnownSource Source { get; set; }
        public ClientWellKnown WellKnown { get; set; }
        public List<WellKnownResolverService.WellKnownResolutionWarning> Warnings { get; set; }
    }
}