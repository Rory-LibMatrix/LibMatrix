using System.Text.Json.Serialization;

namespace LibMatrix.Federation.FederationTypes;

public class FederationGetMissingEventsRequest {
    /// <summary>
    /// Latest event IDs we already have (aka earliest to return)
    /// </summary>
    [JsonPropertyName("earliest_events")]
    public required List<string> EarliestEvents { get; set; }

    /// <summary>
    /// Events we want to get events before
    /// </summary>
    [JsonPropertyName("latest_events")]
    public required List<string> LatestEvents { get; set; }

    /// <summary>
    /// 10 by default
    /// </summary>
    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    /// <summary>
    /// 0 by default
    /// </summary>
    [JsonPropertyName("min_depth")]
    public long MinDepth { get; set; }
}

public class FederationGetMissingEventsResponse {
    [JsonPropertyName("events")]
    public required List<SignedFederationEvent> Events { get; set; }
}