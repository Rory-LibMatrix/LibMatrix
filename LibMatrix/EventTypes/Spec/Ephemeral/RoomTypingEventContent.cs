using System.Text.Json.Serialization;
using LibMatrix.Interfaces;

namespace LibMatrix.EventTypes.Spec.State;

[MatrixEvent(EventName = "m.typing")]
public class RoomTypingEventContent : TimelineEventContent {
    [JsonPropertyName("user_ids")]
    public string[]? UserIds { get; set; }
}
