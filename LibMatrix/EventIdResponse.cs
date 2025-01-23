using System.Text.Json.Serialization;

namespace LibMatrix;

public class EventIdResponse {
    [JsonPropertyName("event_id")]
    public required string EventId { get; set; }
}