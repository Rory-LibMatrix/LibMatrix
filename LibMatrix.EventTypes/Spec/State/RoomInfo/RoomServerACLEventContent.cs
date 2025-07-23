using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace LibMatrix.EventTypes.Spec.State.RoomInfo;

[MatrixEvent(EventName = EventId)]
public class RoomServerAclEventContent : EventContent {
    public const string EventId = "m.room.server_acl";

    [JsonPropertyName("allow")]
    public List<string>? Allow { get; set; }

    [JsonPropertyName("deny")]
    public List<string>? Deny { get; set; }

    [JsonPropertyName("allow_ip_literals")]
    public bool AllowIpLiterals { get; set; } // = false;

    [JsonIgnore]
    public List<Regex>? AllowRegexes => Allow?.ConvertAll(pattern => new Regex(pattern.Replace(".", "\\.").Replace("*", ".*").Replace("?", "."), RegexOptions.Compiled)) ?? [];

    [JsonIgnore]
    public List<Regex>? DenyRegexes => Deny?.ConvertAll(pattern => new Regex(pattern.Replace(".", "\\.").Replace("*", ".*").Replace("?", "."), RegexOptions.Compiled)) ?? [];
}