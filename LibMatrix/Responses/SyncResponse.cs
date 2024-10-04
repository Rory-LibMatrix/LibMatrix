using System.Text.Json.Serialization;

namespace LibMatrix.Responses;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(SyncResponse))]
internal partial class SyncResponseSerializerContext : JsonSerializerContext { }

public class SyncResponse {
    [JsonPropertyName("next_batch")]
    public string NextBatch { get; set; } = null!;

    [JsonPropertyName("account_data")]
    public EventList? AccountData { get; set; }

    [JsonPropertyName("presence")]
    public PresenceDataStructure? Presence { get; set; }

    [JsonPropertyName("device_one_time_keys_count")]
    public Dictionary<string, int>? DeviceOneTimeKeysCount { get; set; } = null!;

    [JsonPropertyName("rooms")]
    public RoomsDataStructure? Rooms { get; set; }

    [JsonPropertyName("to_device")]
    public EventList? ToDevice { get; set; }

    [JsonPropertyName("device_lists")]
    public DeviceListsDataStructure? DeviceLists { get; set; }

    public class DeviceListsDataStructure {
        [JsonPropertyName("changed")]
        public List<string>? Changed { get; set; }

        [JsonPropertyName("left")]
        public List<string>? Left { get; set; }
    }

    // supporting classes
    public class PresenceDataStructure {
        [JsonPropertyName("events")]
        public List<StateEventResponse>? Events { get; set; }
    }

    public class RoomsDataStructure {
        [JsonPropertyName("join")]
        public Dictionary<string, JoinedRoomDataStructure>? Join { get; set; }

        [JsonPropertyName("invite")]
        public Dictionary<string, InvitedRoomDataStructure>? Invite { get; set; }

        [JsonPropertyName("leave")]
        public Dictionary<string, LeftRoomDataStructure>? Leave { get; set; }

        public class LeftRoomDataStructure {
            [JsonPropertyName("account_data")]
            public EventList? AccountData { get; set; }

            [JsonPropertyName("timeline")]
            public JoinedRoomDataStructure.TimelineDataStructure? Timeline { get; set; }

            [JsonPropertyName("state")]
            public EventList? State { get; set; }
        }

        public class JoinedRoomDataStructure {
            [JsonPropertyName("timeline")]
            public TimelineDataStructure? Timeline { get; set; }

            [JsonPropertyName("state")]
            public EventList? State { get; set; }

            [JsonPropertyName("account_data")]
            public EventList? AccountData { get; set; }

            [JsonPropertyName("ephemeral")]
            public EventList? Ephemeral { get; set; }

            [JsonPropertyName("unread_notifications")]
            public UnreadNotificationsDataStructure? UnreadNotifications { get; set; }

            [JsonPropertyName("summary")]
            public SummaryDataStructure? Summary { get; set; }

            public class TimelineDataStructure {
                public TimelineDataStructure() { }

                public TimelineDataStructure(List<StateEventResponse>? events, bool? limited) {
                    Events = events;
                    Limited = limited;
                }

                [JsonPropertyName("events")]
                public List<StateEventResponse>? Events { get; set; }

                [JsonPropertyName("prev_batch")]
                public string? PrevBatch { get; set; }

                [JsonPropertyName("limited")]
                public bool? Limited { get; set; }
            }

            public class UnreadNotificationsDataStructure {
                [JsonPropertyName("notification_count")]
                public int NotificationCount { get; set; }

                [JsonPropertyName("highlight_count")]
                public int HighlightCount { get; set; }
            }

            public class SummaryDataStructure {
                [JsonPropertyName("m.heroes")]
                public List<string>? Heroes { get; set; }

                [JsonPropertyName("m.invited_member_count")]
                public int? InvitedMemberCount { get; set; }

                [JsonPropertyName("m.joined_member_count")]
                public int? JoinedMemberCount { get; set; }
            }
        }

        public class InvitedRoomDataStructure {
            [JsonPropertyName("invite_state")]
            public EventList? InviteState { get; set; }
        }
    }
}