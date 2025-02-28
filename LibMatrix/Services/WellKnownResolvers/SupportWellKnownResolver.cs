using System.Text.Json.Serialization;
using ArcaneLibs.Collections;
using LibMatrix.Extensions;
using Microsoft.Extensions.Logging;

namespace LibMatrix.Services.WellKnownResolvers;

public class SupportWellKnownResolver(ILogger<SupportWellKnownResolver> logger) {
    private static readonly SemaphoreCache<WellKnownResolverService.WellKnownResolutionResult<SupportWellKnown>> ClientWellKnownCache = new() {
        StoreNulls = false
    };

    private static readonly MatrixHttpClient HttpClient = new();

    public Task<WellKnownResolverService.WellKnownResolutionResult<SupportWellKnown>> TryResolveClientWellKnown(string homeserver) {
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

    private async Task<SupportWellKnown?> TryGetClientWellKnownFromHttps(string homeserver) {
        try {
            return await HttpClient.TryGetFromJsonAsync<SupportWellKnown>($"https://{homeserver}/.well-known/matrix/support");
        }
        catch {
            return null;
        }
    }

    public struct SupportWellKnown {
        [JsonPropertyName("contacts")]
        public List<WellKnownContact>? Contacts { get; set; }

        [JsonPropertyName("support_page")]
        public Uri? SupportPage { get; set; }

        public class WellKnownContact {
            [JsonPropertyName("email_address")]
            public string? EmailAddress { get; set; }

            [JsonPropertyName("matrix_id")]
            public string? MatrixId { get; set; }

            [JsonPropertyName("role")]
            public required string Role { get; set; }
        }
    }
}