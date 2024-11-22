using System.Text.Json.Serialization;

namespace LibMatrix.EventTypes.Interop.Draupnir;

[MatrixEvent(EventName = EventId)]
public class DraupnirProtectedRoomsData : EventContent {
    public const string EventId = "org.matrix.mjolnir.protected_rooms";

    [JsonPropertyName("rooms")]
    public List<string> Rooms { get; set; } = new();
}