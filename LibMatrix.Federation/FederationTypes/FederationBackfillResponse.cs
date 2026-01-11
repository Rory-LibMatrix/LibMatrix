using System.Text.Json.Serialization;

namespace LibMatrix.Federation.FederationTypes;

public class FederationBackfillResponse {
    [JsonPropertyName("origin")]
    public required string Origin { get; set; }

    [JsonPropertyName("origin_server_ts")]
    public required long OriginServerTs { get; set; }

    [JsonPropertyName("pdus")]
    public required List<SignedFederationEvent> Pdus { get; set; }
}