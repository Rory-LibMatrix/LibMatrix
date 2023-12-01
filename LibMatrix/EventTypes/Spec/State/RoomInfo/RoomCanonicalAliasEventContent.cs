using System.Text.Json.Serialization;
using LibMatrix.Interfaces;

namespace LibMatrix.EventTypes.Spec.State;

[MatrixEvent(EventName = EventId)]
public class RoomCanonicalAliasEventContent : TimelineEventContent {
    public const string EventId = "m.room.canonical_alias";

    [JsonPropertyName("alias")]
    public string? Alias { get; set; }

    [JsonPropertyName("alt_aliases")]
    public string[]? AltAliases { get; set; }
}
