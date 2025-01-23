using System.Text.Json.Serialization;

namespace LibMatrix.EventTypes.Spec.State.RoomInfo;

[MatrixEvent(EventName = EventId)]
public class RoomCanonicalAliasEventContent : EventContent {
    public const string EventId = "m.room.canonical_alias";

    [JsonPropertyName("alias")]
    public string? Alias { get; set; }

    [JsonPropertyName("alt_aliases")]
    public string[]? AltAliases { get; set; }
}