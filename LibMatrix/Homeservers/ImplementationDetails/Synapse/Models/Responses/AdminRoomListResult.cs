using System.Text.Json.Serialization;

namespace LibMatrix.Homeservers.ImplementationDetails.Synapse.Models.Responses;

public class AdminRoomListResult {
    [JsonPropertyName("offset")]
    public int Offset { get; set; }

    [JsonPropertyName("total_rooms")]
    public int TotalRooms { get; set; }

    [JsonPropertyName("next_batch")]
    public int? NextBatch { get; set; }

    [JsonPropertyName("prev_batch")]
    public int? PrevBatch { get; set; }

    [JsonPropertyName("rooms")]
    public List<AdminRoomListResultRoom> Rooms { get; set; } = new();

    public class AdminRoomListResultRoom {
        [JsonPropertyName("room_id")]
        public required string RoomId { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("canonical_alias")]
        public string? CanonicalAlias { get; set; }

        [JsonPropertyName("joined_members")]
        public int JoinedMembers { get; set; }

        [JsonPropertyName("joined_local_members")]
        public int JoinedLocalMembers { get; set; }

        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [JsonPropertyName("creator")]
        public string? Creator { get; set; }

        [JsonPropertyName("encryption")]
        public string? Encryption { get; set; }

        [JsonPropertyName("federatable")]
        public bool Federatable { get; set; }

        [JsonPropertyName("public")]
        public bool Public { get; set; }

        [JsonPropertyName("join_rules")]
        public string? JoinRules { get; set; }

        [JsonPropertyName("guest_access")]
        public string? GuestAccess { get; set; }

        [JsonPropertyName("history_visibility")]
        public string? HistoryVisibility { get; set; }

        [JsonPropertyName("state_events")]
        public int StateEvents { get; set; }
    }
}