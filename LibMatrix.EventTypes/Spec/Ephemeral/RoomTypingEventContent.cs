using System.Text.Json.Serialization;

namespace LibMatrix.EventTypes.Spec.State;

[MatrixEvent(EventName = EventId)]
public class RoomTypingEventContent : EventContent {
    public const string EventId = "m.typing";

    [JsonPropertyName("user_ids")]
    public string[]? UserIds { get; set; }
}