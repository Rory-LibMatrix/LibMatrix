using System.Text.Json.Serialization;

namespace LibMatrix.EventTypes.Spec.State.RoomInfo;

[MatrixEvent(EventName = EventId)]
public class RoomPolicyServerEventContent : EventContent {
    public const string EventId = "org.matrix.msc4284.policy";

    [JsonPropertyName("via")]
    public string? Via { get; set; }

    [JsonPropertyName("public_key")]
    public string? PublicKey { get; set; }
}