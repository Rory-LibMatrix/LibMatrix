using System.Text.Json.Serialization;

namespace LibMatrix.EventTypes.Spec.State.RoomInfo;

[MatrixEvent(EventName = EventId)]
public class RoomHistoryVisibilityEventContent : EventContent {
    public const string EventId = "m.room.history_visibility";

    [JsonPropertyName("history_visibility")]
    public required string HistoryVisibility { get; set; }
}