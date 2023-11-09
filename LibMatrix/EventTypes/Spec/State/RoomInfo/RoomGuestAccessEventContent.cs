using System.Text.Json.Serialization;
using LibMatrix.Interfaces;

namespace LibMatrix.EventTypes.Spec.State;

[MatrixEvent(EventName = "m.room.guest_access")]
public class RoomGuestAccessEventContent : TimelineEventContent {
    [JsonPropertyName("guest_access")]
    public string GuestAccess { get; set; }

    public bool IsGuestAccessEnabled {
        get => GuestAccess == "can_join";
        set => GuestAccess = value ? "can_join" : "forbidden";
    }
}
