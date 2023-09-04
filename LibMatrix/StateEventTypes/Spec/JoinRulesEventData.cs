using System.Collections.Generic;
using System.Text.Json.Serialization;
using LibMatrix.Extensions;
using LibMatrix.Helpers;
using LibMatrix.Interfaces;

namespace LibMatrix.StateEventTypes.Spec;

[MatrixEvent(EventName = "m.room.join_rules")]
public class JoinRulesEventData : IStateEventType {
    private static string Public = "public";
    private static string Invite = "invite";
    private static string Knock = "knock";

    [JsonPropertyName("join_rule")]
    public string JoinRule { get; set; }

    [JsonPropertyName("allow")]
    public List<AllowEntry> Allow { get; set; }

    public class AllowEntry {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("room_id")]
        public string RoomId { get; set; }
    }
}
