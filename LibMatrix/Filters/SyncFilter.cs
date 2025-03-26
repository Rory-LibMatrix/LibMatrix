using System.Text.Json.Serialization;

namespace LibMatrix.Filters;

public class SyncFilter {
    [JsonPropertyName("account_data")]
    public EventFilter? AccountData { get; set; }

    [JsonPropertyName("presence")]
    public EventFilter? Presence { get; set; }

    [JsonPropertyName("room")]
    public RoomFilter? Room { get; set; }

    public class RoomFilter {
        [JsonPropertyName("account_data")]
        public StateFilter? AccountData { get; set; }

        [JsonPropertyName("ephemeral")]
        public StateFilter? Ephemeral { get; set; }

        [JsonPropertyName("state")]
        public StateFilter? State { get; set; }

        [JsonPropertyName("timeline")]
        public StateFilter? Timeline { get; set; }

        [JsonPropertyName("rooms")]
        public List<string>? Rooms { get; set; }

        [JsonPropertyName("not_rooms")]
        public List<string>? NotRooms { get; set; }

        [JsonPropertyName("include_leave")]
        public bool? IncludeLeave { get; set; }

        private static readonly RoomFilter Empty = new() {
            Rooms = [],
            IncludeLeave = false,
            AccountData = StateFilter.Empty,
            Ephemeral = StateFilter.Empty,
            State = StateFilter.Empty,
            Timeline = StateFilter.Empty,
        };

        public class StateFilter(
            bool? containsUrl = null,
            bool? includeRedundantMembers = null,
            bool? lazyLoadMembers = null,
            List<string>? rooms = null,
            List<string>? notRooms = null,
            bool? unreadThreadNotifications = null,
            //base ctor
            int? limit = null,
            List<string>? types = null,
            List<string>? notTypes = null,
            List<string>? senders = null,
            List<string>? notSenders = null
        ) : EventFilter(limit, types, notTypes, senders, notSenders) {
            [JsonPropertyName("contains_url")]
            public bool? ContainsUrl { get; set; } = containsUrl;

            [JsonPropertyName("include_redundant_members")]
            public bool? IncludeRedundantMembers { get; set; } = includeRedundantMembers;

            [JsonPropertyName("lazy_load_members")]
            public bool? LazyLoadMembers { get; set; } = lazyLoadMembers;

            [JsonPropertyName("rooms")]
            public List<string>? Rooms { get; set; } = rooms;

            [JsonPropertyName("not_rooms")]
            public List<string>? NotRooms { get; set; } = notRooms;

            [JsonPropertyName("unread_thread_notifications")]
            public bool? UnreadThreadNotifications { get; set; } = unreadThreadNotifications;

            public static readonly StateFilter Empty = new(limit: 0, senders: [], types: [], rooms: []);
        }
    }

    public class EventFilter(int? limit = null, List<string>? types = null, List<string>? notTypes = null, List<string>? senders = null, List<string>? notSenders = null) {
        [JsonPropertyName("limit")]
        public int? Limit { get; set; } = limit;

        [JsonPropertyName("types")]
        public List<string>? Types { get; set; } = types;

        [JsonPropertyName("not_types")]
        public List<string>? NotTypes { get; set; } = notTypes;

        [JsonPropertyName("senders")]
        public List<string>? Senders { get; set; } = senders;

        [JsonPropertyName("not_senders")]
        public List<string>? NotSenders { get; set; } = notSenders;

        public static readonly EventFilter Empty = new(limit: 0, senders: [], types: []);
    }
}
