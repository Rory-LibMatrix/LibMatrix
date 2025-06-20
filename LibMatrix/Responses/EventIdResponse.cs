using System.Text.Json.Serialization;

namespace LibMatrix.Responses;

public class EventIdResponse {
    [JsonPropertyName("event_id")]
    public required string EventId { get; set; }
}