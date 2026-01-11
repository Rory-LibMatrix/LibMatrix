using System.Text.Json.Serialization;

namespace LibMatrix.Federation.FederationTypes;

public class FederationEvent : MatrixEventResponse {
    [JsonPropertyName("auth_events")]
    public required List<string> AuthEvents { get; set; } = [];

    [JsonPropertyName("prev_events")]
    public required List<string> PrevEvents { get; set; } = [];

    [JsonPropertyName("depth")]
    public required int Depth { get; set; }
}

public class SignedFederationEvent : FederationEvent {
    [JsonPropertyName("signatures")]
    public required Dictionary<string, Dictionary<string, string>> Signatures { get; set; } = new();

    [JsonPropertyName("hashes")]
    public required Dictionary<string, string> Hashes { get; set; } = new();
}

public class FederationEphemeralEvent {
    [JsonPropertyName("edu_type")]
    public required string Type { get; set; }

    [JsonPropertyName("content")]
    public required Dictionary<string, object> Content { get; set; } = new();
}