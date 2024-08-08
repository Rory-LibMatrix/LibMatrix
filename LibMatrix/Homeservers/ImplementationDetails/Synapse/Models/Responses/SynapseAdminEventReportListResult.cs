using System.Text.Json.Serialization;

namespace LibMatrix.Homeservers.ImplementationDetails.Synapse.Models.Responses;

public class SynapseAdminEventReportListResult {
    [JsonPropertyName("offset")]
    public int Offset { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("next_token")]
    public string? NextToken { get; set; }

    [JsonPropertyName("event_reports")]
    public List<SynapseAdminEventReportListResultReport> Reports { get; set; } = new();

    public class SynapseAdminEventReportListResultReport {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("is_guest")]
        public bool? IsGuest { get; set; }

        [JsonPropertyName("admin")]
        public bool? Admin { get; set; }

        [JsonPropertyName("user_type")]
        public string? UserType { get; set; }

        [JsonPropertyName("deactivated")]
        public bool Deactivated { get; set; }

        [JsonPropertyName("erased")]
        public bool Erased { get; set; }

        [JsonPropertyName("shadow_banned")]
        public bool ShadowBanned { get; set; }

        [JsonPropertyName("displayname")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("avatar_url")]
        public string? AvatarUrl { get; set; }

        [JsonPropertyName("creation_ts")]
        public long CreationTs { get; set; }

        [JsonPropertyName("last_seen_ts")]
        public long? LastSeenTs { get; set; }

        [JsonPropertyName("locked")]
        public bool Locked { get; set; }

        [JsonPropertyName("approved")]
        public bool Approved { get; set; }
    }
}