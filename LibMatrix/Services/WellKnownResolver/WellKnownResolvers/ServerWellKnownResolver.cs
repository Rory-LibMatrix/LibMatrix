using System.Text.Json.Serialization;
using ArcaneLibs.Collections;
using Microsoft.Extensions.Logging;
using WellKnownType = LibMatrix.Services.WellKnownResolver.WellKnownResolvers.ServerWellKnown;
using ResultType =
    LibMatrix.Services.WellKnownResolver.WellKnownResolverService.WellKnownResolutionResult<LibMatrix.Services.WellKnownResolver.WellKnownResolvers.ServerWellKnown?>;

namespace LibMatrix.Services.WellKnownResolver.WellKnownResolvers;

public class ServerWellKnownResolver(ILogger<ServerWellKnownResolver> logger, WellKnownResolverConfiguration configuration)
    : BaseWellKnownResolver<ServerWellKnown> {
    private static readonly SemaphoreCache<WellKnownResolverService.WellKnownResolutionResult<ServerWellKnown>> ClientWellKnownCache = new() {
        StoreNulls = false
    };

    public Task<WellKnownResolverService.WellKnownResolutionResult<ServerWellKnown>> TryResolveWellKnown(string homeserver, WellKnownResolverConfiguration? config = null) {
        config ??= configuration;
        return ClientWellKnownCache.TryGetOrAdd(homeserver, async () => {
            logger.LogTrace($"Resolving client well-known: {homeserver}");

            WellKnownResolverService.WellKnownResolutionResult<ServerWellKnown> result =
                await TryGetWellKnownFromUrl($"https://{homeserver}/.well-known/matrix/server", WellKnownResolverService.WellKnownSource.Https);
            if (result.Content != null) return result;

            return result;
        });
    }
}

public class ServerWellKnown {
    [JsonPropertyName("m.server")]
    public string Homeserver { get; set; }
}