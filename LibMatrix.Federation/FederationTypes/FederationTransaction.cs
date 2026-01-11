using System.Text.Json.Serialization;

namespace LibMatrix.Federation.FederationTypes;

/// <summary>
/// This only covers v12 rooms for now?
/// </summary>
public class FederationTransaction {
    /// <summary>
    /// Up to 100 EDUs per transaction
    /// </summary>
    [JsonPropertyName("edus")]
    public List<FederationEvent>? EphemeralEvents { get; set; }

    [JsonPropertyName("origin")]
    public required string Origin { get; set; }

    [JsonPropertyName("origin_server_ts")]
    public required long OriginServerTs { get; set; }

    /// <summary>
    /// Up to 50 PDUs per transaction
    /// </summary>
    [JsonPropertyName("pdus")]
    public List<SignedFederationEvent>? PersistentEvents { get; set; }
}