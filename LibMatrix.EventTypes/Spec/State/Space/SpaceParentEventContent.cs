using System.Text.Json.Serialization;

namespace LibMatrix.EventTypes.Spec.State.Space;

[MatrixEvent(EventName = EventId)]
public class SpaceParentEventContent : EventContent {
    public const string EventId = "m.space.parent";

    [JsonPropertyName("via")]
    public string[]? Via { get; set; }

    [JsonPropertyName("canonical")]
    public bool? Canonical { get; set; }
}