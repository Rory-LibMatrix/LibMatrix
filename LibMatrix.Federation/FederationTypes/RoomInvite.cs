using System.Text.Json.Serialization;

namespace LibMatrix.Federation.FederationTypes;

public class RoomInvite {
    [JsonPropertyName("event")]
    public required SignedFederationEvent Event { get; set; }

    [JsonPropertyName("invite_room_state")]
    public required List<MatrixEventResponse> InviteRoomState { get; set; } = [];

    [JsonPropertyName("room_version")]
    public required string RoomVersion { get; set; }
}