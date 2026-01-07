using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using WellKnownType = LibMatrix.Services.WellKnownResolver.WellKnownResolvers.PolicyServerWellKnown;
using ResultType = LibMatrix.Services.WellKnownResolver.WellKnownResolverService.WellKnownResolutionResult<
    LibMatrix.Services.WellKnownResolver.WellKnownResolvers.PolicyServerWellKnown?
>;

namespace LibMatrix.Services.WellKnownResolver.WellKnownResolvers;

public class PolicyServerWellKnownResolver(ILogger<PolicyServerWellKnownResolver> logger, WellKnownResolverConfiguration configuration) : BaseWellKnownResolver<WellKnownType> {
    public Task<ResultType> TryResolveWellKnown(string homeserver, WellKnownResolverConfiguration? config = null) {
        config ??= configuration;
        return WellKnownCache.TryGetOrAdd(homeserver, async () => {
            logger.LogTrace($"Resolving support well-known: {homeserver}");

            ResultType result = await TryGetWellKnownFromUrl($"https://{homeserver}/.well-known/matrix/policy_server", WellKnownResolverService.WellKnownSource.Https);
            if (result.Content != null)
                return result;

            return null;
        });
    }
}

public class PolicyServerWellKnown {
    [JsonPropertyName("public_key")]
    public string PublicKey { get; set; } = null!;
}