using System.Text.Json.Serialization;

namespace LibMatrix.Homeservers.ImplementationDetails.Synapse.Models.Responses;

public class SynapseAdminRegistrationTokenUpdateRequest {
    [JsonPropertyName("uses_allowed")]
    public int? UsesAllowed { get; set; }

    [JsonPropertyName("expiry_time")]
    public long? ExpiryTime { get; set; }

    [JsonIgnore]
    public DateTime? ExpiresAt {
        get => ExpiryTime.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(ExpiryTime.Value).DateTime : null;
        set => ExpiryTime = value.HasValue ? new DateTimeOffset(value.Value).ToUnixTimeMilliseconds() : null;
    }

    [JsonIgnore]
    public TimeSpan? ExpiresAfter {
        get => ExpiryTime.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(ExpiryTime.Value).DateTime - DateTimeOffset.Now : null;
        set => ExpiryTime = value.HasValue ? (DateTimeOffset.Now + value.Value).ToUnixTimeMilliseconds() : null;
    }
}

public class SynapseAdminRegistrationTokenCreateRequest : SynapseAdminRegistrationTokenUpdateRequest {
    [JsonPropertyName("token")]
    public string? Token { get; set; }

    [JsonPropertyName("length")]
    public int? Length { get; set; }
}