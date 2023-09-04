using System.Text.Json.Serialization;
using LibMatrix.Extensions;
using LibMatrix.Helpers;
using LibMatrix.Interfaces;

namespace LibMatrix.StateEventTypes.Spec;

[MatrixEvent(EventName = "m.room.canonical_alias")]
public class CanonicalAliasEventData : IStateEventType {
    [JsonPropertyName("alias")]
    public string? Alias { get; set; }
    [JsonPropertyName("alt_aliases")]
    public string[]? AltAliases { get; set; }
}
