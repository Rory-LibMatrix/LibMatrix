using System.Text.Json.Serialization;

namespace LibMatrix.EventTypes.Spec.State.RoomInfo;

[MatrixEvent(EventName = EventId)]
public class RoomHistoryVisibilityEventContent : EventContent {
    public const string EventId = "m.room.history_visibility";

    [JsonPropertyName("history_visibility")]
    public required string HistoryVisibility { get; set; }
    
    public static class HistoryVisibilityTypes {
        public const string WorldReadable = "world_readable";
        public const string Invited = "invited";
        public const string Shared = "shared";
        public const string Joined = "joined";
    }
}