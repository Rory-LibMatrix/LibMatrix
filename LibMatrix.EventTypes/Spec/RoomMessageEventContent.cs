using System.Text.Json.Serialization;

namespace LibMatrix.EventTypes.Spec;

[MatrixEvent(EventName = EventId)]
public class RoomMessageEventContent : TimelineEventContent {
    public const string EventId = "m.room.message";

    public RoomMessageEventContent(string messageType = "m.notice", string? body = null) {
        MessageType = messageType;
        Body = body ?? "";
    }

    // TODO: https://spec.matrix.org/v1.16/client-server-api/#mimage
    // TODO: add `file` for e2ee files

    [JsonPropertyName("body")]
    public string Body { get; set; }

    [JsonPropertyName("msgtype")]
    public string MessageType { get; set; } = "m.notice";

    [JsonPropertyName("formatted_body")]
    public string? FormattedBody { get; set; }

    [JsonPropertyName("format")]
    public string? Format { get; set; }

    /// <summary>
    /// Media URI for this message, if any
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("filename")]
    public string? FileName { get; set; }

    [JsonPropertyName("info")]
    public FileInfoStruct? FileInfo { get; set; }

    [JsonIgnore]
    public string BodyWithoutReplyFallback {
        get {
            var parts = Body
                .Split('\n')
                .SkipWhile(x => x.StartsWith(">"))
                .SkipWhile(x => x.Trim().Length == 0)
                .ToList();
            return parts.Count > 0 ? parts.Aggregate((x, y) => $"{x}\n{y}") : Body;
        }
    }

    [JsonPropertyName("m.mentions")]
    public MentionsStruct? Mentions { get; set; }

    public class MentionsStruct {
        [JsonPropertyName("user_ids")]
        public List<string>? Users { get; set; }

        [JsonPropertyName("room")]
        public bool? Room { get; set; }
    }

    public class FileInfoStruct {
        [JsonPropertyName("mimetype")]
        public string? MimeType { get; set; }

        [JsonPropertyName("size")]
        public long Size { get; set; }

        [JsonPropertyName("thumbnail_url")]
        public string? ThumbnailUrl { get; set; }

        [JsonPropertyName("thumbnail_info")]
        public ThumbnailInfoStruct? ThumbnailInfo { get; set; }

        [JsonPropertyName("w")]
        public int? Width { get; set; }

        [JsonPropertyName("h")]
        public int? Height { get; set; }

        /// <summary>
        ///     Duration of the audio/video in milliseconds, if applicable
        /// </summary>
        [JsonPropertyName("duration")]
        public long? Duration { get; set; }

        public class ThumbnailInfoStruct {
            [JsonPropertyName("w")]
            public int? Width { get; set; }

            [JsonPropertyName("h")]
            public int? Height { get; set; }

            [JsonPropertyName("mimetype")]
            public string? MimeType { get; set; }

            [JsonPropertyName("size")]
            public long? Size { get; set; }
        }
    }
}