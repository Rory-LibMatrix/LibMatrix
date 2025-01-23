using System.Text.Json.Serialization;

namespace LibMatrix.EventTypes.Spec.State.Space;

[MatrixEvent(EventName = EventId)]
public class SpaceChildEventContent : EventContent {
    public const string EventId = "m.space.child";

    [JsonPropertyName("auto_join")]
    public bool? AutoJoin { get; set; }

    [JsonPropertyName("via")]
    public List<string>? Via { get; set; }

    [JsonPropertyName("suggested")]
    public bool? Suggested { get; set; }
}