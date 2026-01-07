using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using WellKnownType = LibMatrix.Services.WellKnownResolver.WellKnownResolvers.SupportWellKnown;
using ResultType = LibMatrix.Services.WellKnownResolver.WellKnownResolverService.WellKnownResolutionResult<
    LibMatrix.Services.WellKnownResolver.WellKnownResolvers.SupportWellKnown?
>;

namespace LibMatrix.Services.WellKnownResolver.WellKnownResolvers;

public class SupportWellKnownResolver(ILogger<SupportWellKnownResolver> logger, WellKnownResolverConfiguration configuration) : BaseWellKnownResolver<WellKnownType> {
    public Task<ResultType> TryResolveWellKnown(string homeserver, WellKnownResolverConfiguration? config = null) {
        config ??= configuration;
        return WellKnownCache.TryGetOrAdd(homeserver, async () => {
            logger.LogTrace($"Resolving support well-known: {homeserver}");

            ResultType result = await TryGetWellKnownFromUrl($"https://{homeserver}/.well-known/matrix/support", WellKnownResolverService.WellKnownSource.Https);
            if (result.Content != null)
                return result;

            return null;
        });
    }
}

public class SupportWellKnown {
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