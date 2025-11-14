using System.Text.Json.Serialization;
using LibMatrix.EventTypes.Spec.State.RoomInfo;

namespace LibMatrix.Homeservers.ImplementationDetails.Synapse.Models.Responses;

public class SynapseAdminRoomListResult {
    [JsonPropertyName("offset")]
    public int Offset { get; set; }

    [JsonPropertyName("total_rooms")]
    public int TotalRooms { get; set; }

    [JsonPropertyName("next_batch")]
    public int? NextBatch { get; set; }

    [JsonPropertyName("prev_batch")]
    public int? PrevBatch { get; set; }

    [JsonPropertyName("rooms")]
    public List<SynapseAdminRoomListResultRoom> Rooms { get; set; } = new();

    public class SynapseAdminRoomListResultRoom {
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

        [JsonPropertyName("gay.rory.synapse_admin_extensions.tombstone")]
        public MatrixEventResponse? TombstoneEvent { get; set; }

        [JsonPropertyName("gay.rory.synapse_admin_extensions.create")]
        public MatrixEventResponse? CreateEvent { get; set; }

        [JsonPropertyName("gay.rory.synapse_admin_extensions.topic")]
        public MatrixEventResponse? TopicEvent { get; set; }

        public async Task<MatrixEventResponse?> GetCreateEventAsync(AuthenticatedHomeserverSynapse hs) {
            if (CreateEvent != null) return CreateEvent;

            try {
                var events = (await hs.Admin.GetRoomStateAsync(RoomId, RoomCreateEventContent.EventId));
                CreateEvent = events.Events.SingleOrDefault(x => x.StateKey == "");
            }
            catch (Exception e) {
                Console.WriteLine($"Failed to fetch room create event for {RoomId}: {e}");
            }

            return null;
        }

        public async Task<MatrixEventResponse?> GetTombstoneEventAsync(AuthenticatedHomeserverSynapse hs) {
            if (TombstoneEvent != null) return TombstoneEvent;

            try {
                var events = (await hs.Admin.GetRoomStateAsync(RoomId, RoomTombstoneEventContent.EventId));
                TombstoneEvent = events.Events.SingleOrDefault(x => x.StateKey == "");
            }
            catch (Exception e) {
                Console.WriteLine($"Failed to fetch room tombstone event for {RoomId}: {e}");
            }

            return null;
        }

        public async Task<MatrixEventResponse?> GetTopicEventAsync(AuthenticatedHomeserverSynapse hs) {
            if (TopicEvent != null) return TopicEvent;

            try {
                var events = await hs.Admin.GetRoomStateAsync(RoomId, RoomTopicEventContent.EventId);
                TopicEvent = events.Events.SingleOrDefault(x => x.StateKey == "");
            }
            catch (Exception e) {
                Console.WriteLine($"Failed to fetch room topic event for {RoomId}: {e}");
            }

            return null;
        }
    }
}