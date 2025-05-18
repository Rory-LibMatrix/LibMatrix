using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace LibMatrix.EventTypes.Spec.State.RoomInfo;

[MatrixEvent(EventName = EventId)]
public class RoomRedactionEventContent : EventContent {
    public const string EventId = "m.room.redaction";

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    /// <summary>
    /// Required in room version 11
    /// </summary>
    [JsonPropertyName("redacts")]
    public string? Redacts { get; set; }
}