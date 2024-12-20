using System.Text.Json.Serialization;

namespace LibMatrix.EventTypes.Spec.State;

[MatrixEvent(EventName = EventId)]
public class RoomHistoryVisibilityEventContent : EventContent {
    public const string EventId = "m.room.history_visibility";

    [JsonPropertyName("history_visibility")]
    public string HistoryVisibility { get; set; }
}