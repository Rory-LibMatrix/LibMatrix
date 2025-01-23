using System.Text.Json.Serialization;

namespace LibMatrix.EventTypes.Spec.State.RoomInfo;

[MatrixEvent(EventName = EventId)]
public class RoomGuestAccessEventContent : EventContent {
    public const string EventId = "m.room.guest_access";

    [JsonPropertyName("guest_access")]
    public required string GuestAccess { get; set; }

    [JsonIgnore]
    public bool IsGuestAccessEnabled {
        get => GuestAccess == "can_join";
        set => GuestAccess = value ? "can_join" : "forbidden";
    }
}