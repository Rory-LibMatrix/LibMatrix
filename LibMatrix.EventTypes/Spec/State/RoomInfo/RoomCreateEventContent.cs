using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace LibMatrix.EventTypes.Spec.State.RoomInfo;

[MatrixEvent(EventName = EventId)]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global", Justification = "Deserialization, public API")]
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global", Justification = "Deserialization, public API")]
public class RoomCreateEventContent : EventContent {
    public const string EventId = "m.room.create";

    [JsonPropertyName("room_version")]
    public string? RoomVersion { get; set; }

    // missing in room version 11+
    [JsonPropertyName("creator")]
    public string? Creator { get; set; }

    // v12+
    [JsonPropertyName("additional_creators")]
    public List<string>? AdditionalCreators { get; set; }

    [JsonPropertyName("m.federate")]
    public bool? Federate { get; set; }

    // [JsonPropertyName("predecessor")]
    // public RoomCreatePredecessor? Predecessor { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }
}

public class RoomCreatePredecessor {
    [JsonPropertyName("room_id")]
    public string? RoomId { get; set; }

    [JsonPropertyName("event_id")]
    public string? EventId { get; set; }
}