using System.Text.Json.Serialization;

namespace LibMatrix.EventTypes.Spec.State.RoomInfo;

[MatrixEvent(EventName = EventId)]
public class RoomTombstoneEventContent : EventContent {
    public const string EventId = "m.room.tombstone";

    [JsonPropertyName("body")]
    public string Body { get; set; }

    [JsonPropertyName("replacement_room")]
    public string ReplacementRoom { get; set; }
}