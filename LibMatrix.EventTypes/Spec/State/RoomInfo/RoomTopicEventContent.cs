using System.Text.Json.Serialization;

namespace LibMatrix.EventTypes.Spec.State.RoomInfo;

[MatrixEvent(EventName = EventId)]
[MatrixEvent(EventName = "org.matrix.msc3765.topic", Legacy = true)]
public class RoomTopicEventContent : EventContent {
    public const string EventId = "m.room.topic";

    [JsonPropertyName("topic")]
    public string? Topic { get; set; }
}