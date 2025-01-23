using System.Text.Json.Serialization;

namespace LibMatrix.EventTypes.Spec.State.RoomInfo;

[MatrixEvent(EventName = EventId)]
public class RoomJoinRulesEventContent : EventContent {
    public const string EventId = "m.room.join_rules";

    /// <summary>
    /// one of ["public", "invite", "knock", "restricted", "knock_restricted"]
    /// "private" is reserved without implementation!
    /// unknown values are treated as "private"
    /// </summary>
    [JsonPropertyName("join_rule")]
    public string JoinRuleValue { get; set; }

    [JsonIgnore]
    public JoinRules JoinRule {
        get => JoinRuleValue switch {
            "public" => JoinRules.Public,
            "invite" => JoinRules.Invite,
            "knock" => JoinRules.Knock,
            "restricted" => JoinRules.Restricted,
            "knock_restricted" => JoinRules.KnockRestricted,
            _ => JoinRules.Private
        };
        set => JoinRuleValue = value switch {
            JoinRules.Public => "public",
            JoinRules.Invite => "invite",
            JoinRules.Knock => "knock",
            JoinRules.Restricted => "restricted",
            JoinRules.KnockRestricted => "knock_restricted",
            _ => "private"
        };
    }

    [JsonPropertyName("allow")]
    public List<AllowEntry>? Allow { get; set; }

    public class AllowEntry {
        [JsonPropertyName("type")]
        public required string Type { get; set; }

        [JsonPropertyName("room_id")]
        public required string RoomId { get; set; }

        public static class Types {
            public const string RoomMembership = "m.room_membership";
        }
    }

    public enum JoinRules {
        Public,
        Invite,
        Knock,
        Restricted,
        KnockRestricted,
        Private // reserved without implementation!
    }
}