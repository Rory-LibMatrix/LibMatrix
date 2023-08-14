using System.Text.Json.Serialization;
using LibMatrix.Extensions;
using LibMatrix.Interfaces;

namespace LibMatrix.StateEventTypes.Spec;

[MatrixEvent(EventName = "m.room.avatar")]
public class RoomAvatarEventData : IStateEventType {
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("info")]
    public RoomAvatarInfo? Info { get; set; }

    public class RoomAvatarInfo {
        [JsonPropertyName("h")]
        public int? Height { get; set; }

        [JsonPropertyName("w")]
        public int? Width { get; set; }

        [JsonPropertyName("mimetype")]
        public string? MimeType { get; set; }

        [JsonPropertyName("size")]
        public int? Size { get; set; }
    }
}
