using System.Text.Json.Serialization;

namespace LibMatrix.Homeservers.ImplementationDetails.Synapse.Models.Requests;

public class SynapseAdminRoomDeleteRequest {
    [JsonPropertyName("new_room_user_id")]
    public string? NewRoomUserId { get; set; }

    [JsonPropertyName("room_name")]
    public string? RoomName { get; set; }

    [JsonPropertyName("block")]
    public bool Block { get; set; }

    [JsonPropertyName("purge")]
    public bool Purge { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("force_purge")]
    public bool ForcePurge { get; set; }
}