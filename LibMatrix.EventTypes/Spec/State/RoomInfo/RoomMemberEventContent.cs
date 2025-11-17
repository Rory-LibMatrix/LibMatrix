using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace LibMatrix.EventTypes.Spec.State.RoomInfo;

[MatrixEvent(EventName = EventId)]
public class RoomMemberEventContent : EventContent {
    public const string EventId = "m.room.member";

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonPropertyName("membership")]
    public required string Membership { get; set; }

    [JsonPropertyName("displayname")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("is_direct")]
    public bool? IsDirect { get; set; }

    [JsonPropertyName("avatar_url")]
    public string? AvatarUrl { get; set; }

    [JsonPropertyName("kind")]
    public string? Kind { get; set; }

    [JsonPropertyName("join_authorised_via_users_server")]
    public string? JoinAuthorisedViaUsersServer { get; set; }

    [JsonPropertyName("third_party_invite")]
    public ThirdPartyMemberInvite? ThirdPartyInvite { get; set; }

    public class ThirdPartyMemberInvite {
        [JsonPropertyName("display_name")]
        public required string DisplayName { get; set; }

        [JsonPropertyName("signed")]
        public required SignedThirdPartyInvite Signed { get; set; }

        public class SignedThirdPartyInvite {
            [JsonPropertyName("mxid")]
            public required string Mxid { get; set; }

            [JsonPropertyName("signatures")]
            public required Dictionary<string, Dictionary<string, string>> Signatures { get; set; }

            [JsonPropertyName("token")]
            public required string Token { get; set; }
        }
    }

    public static class MembershipTypes {
        public const string Invite = "invite";
        public const string Join = "join";
        public const string Leave = "leave";
        public const string Ban = "ban";
        public const string Knock = "knock";
    }
}