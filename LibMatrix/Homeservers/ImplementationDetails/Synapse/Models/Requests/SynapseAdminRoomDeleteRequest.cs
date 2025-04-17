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

public class SynapseAdminRoomDeleteResponse {
    [JsonPropertyName("delete_id")]
    public string DeleteId { get; set; } = null!;
}

public class SynapseAdminRoomDeleteStatusList {
    [JsonPropertyName("results")]
    public List<SynapseAdminRoomDeleteStatus> Results { get; set; }
}

public class SynapseAdminRoomDeleteStatus {
    public const string Scheduled = "scheduled";
    public const string Active = "active";
    public const string Complete = "complete";
    public const string Failed = "failed";

    [JsonPropertyName("status")]
    public string Status { get; set; } = null!;

    [JsonPropertyName("shutdown_room")]
    public RoomShutdownInfo ShutdownRoom { get; set; }

    public class RoomShutdownInfo {
        [JsonPropertyName("kicked_users")]
        public List<string>? KickedUsers { get; set; }

        [JsonPropertyName("failed_to_kick_users")]
        public List<string>? FailedToKickUsers { get; set; }

        [JsonPropertyName("local_aliases")]
        public List<string>? LocalAliasses { get; set; }

        [JsonPropertyName("new_room_id")]
        public string? NewRoomId { get; set; }
    }
}