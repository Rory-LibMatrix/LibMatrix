using System.Text.Json.Serialization;

namespace LibMatrix.EventTypes.Spec.State.RoomInfo;

[MatrixEvent(EventName = EventId)]
public class RoomEncryptionEventContent : EventContent {
    public const string EventId = "m.room.encryption";

    [JsonPropertyName("algorithm")]
    public string? Algorithm { get; set; }

    [JsonPropertyName("rotation_period_ms")]
    public ulong? RotationPeriodMs { get; set; }

    [JsonPropertyName("rotation_period_msgs")]
    public ulong? RotationPeriodMsgs { get; set; }
}