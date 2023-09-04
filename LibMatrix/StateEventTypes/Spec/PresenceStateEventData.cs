using System.Text.Json.Serialization;
using LibMatrix.Extensions;
using LibMatrix.Helpers;
using LibMatrix.Interfaces;

namespace LibMatrix.StateEventTypes.Spec;

[MatrixEvent(EventName = "m.presence")]
public class PresenceStateEventData : IStateEventType {
    [JsonPropertyName("presence")]
    public string Presence { get; set; }
    [JsonPropertyName("last_active_ago")]
    public long LastActiveAgo { get; set; }
    [JsonPropertyName("currently_active")]
    public bool CurrentlyActive { get; set; }
    [JsonPropertyName("status_msg")]
    public string StatusMessage { get; set; }
    [JsonPropertyName("avatar_url")]
    public string AvatarUrl { get; set; }
    [JsonPropertyName("displayname")]
    public string DisplayName { get; set; }
}
