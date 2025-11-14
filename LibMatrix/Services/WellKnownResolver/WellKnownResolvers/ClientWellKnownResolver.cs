using System.Text.Json.Serialization;
using ArcaneLibs.Collections;
using LibMatrix.Extensions;
using Microsoft.Extensions.Logging;
using WellKnownType = LibMatrix.Services.WellKnownResolver.WellKnownResolvers.ClientWellKnown;
using ResultType =
    LibMatrix.Services.WellKnownResolver.WellKnownResolverService.WellKnownResolutionResult<LibMatrix.Services.WellKnownResolver.WellKnownResolvers.ClientWellKnown?>;

namespace LibMatrix.Services.WellKnownResolver.WellKnownResolvers;

public class ClientWellKnownResolver(ILogger<ClientWellKnownResolver> logger, WellKnownResolverConfiguration configuration)
    : BaseWellKnownResolver<ClientWellKnown> {
    private static readonly SemaphoreCache<WellKnownResolverService.WellKnownResolutionResult<ClientWellKnown>> ClientWellKnownCache = new() {
        StoreNulls = false
    };

    public Task<WellKnownResolverService.WellKnownResolutionResult<ClientWellKnown>> TryResolveWellKnown(string homeserver, WellKnownResolverConfiguration? config = null) {
        config ??= configuration;
        return ClientWellKnownCache.TryGetOrAdd(homeserver, async () => {
            logger.LogTrace($"Resolving client well-known: {homeserver}");

            WellKnownResolverService.WellKnownResolutionResult<ClientWellKnown> result =
                await TryGetWellKnownFromUrl($"https://{homeserver}/.well-known/matrix/client", WellKnownResolverService.WellKnownSource.Https);
            if (result.Content != null) return result;


            return result;
        });
    }
}

public class ClientWellKnown {
    [JsonPropertyName("m.homeserver")]
    public WellKnownHomeserver Homeserver { get; set; }

    public class WellKnownHomeserver {
        [JsonPropertyName("base_url")]
        public required string BaseUrl { get; set; }
    }
}