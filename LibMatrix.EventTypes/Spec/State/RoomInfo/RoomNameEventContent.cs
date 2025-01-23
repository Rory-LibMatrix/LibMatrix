using System.Text.Json.Serialization;

namespace LibMatrix.EventTypes.Spec.State.RoomInfo;

[MatrixEvent(EventName = EventId)]
public class RoomNameEventContent : EventContent {
    public const string EventId = "m.room.name";

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}