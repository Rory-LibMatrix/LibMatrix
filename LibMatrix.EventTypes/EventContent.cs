using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace LibMatrix.EventTypes;

public abstract class EventContent;

public class UnknownEventContent : TimelineEventContent;

public abstract class TimelineEventContent : EventContent {
    [JsonPropertyName("m.relates_to")]
    public MessageRelatesTo? RelatesTo { get; set; }

    [JsonPropertyName("m.new_content")]
    public JsonObject? NewContent { get; set; }

    public TimelineEventContent SetReplaceRelation(string eventId) {
        NewContent = JsonSerializer.SerializeToNode(this, GetType())!.AsObject();
        // NewContent = JsonSerializer.Deserialize(jsonText, GetType());
        RelatesTo = new MessageRelatesTo {
            RelationType = "m.replace",
            EventId = eventId
        };
        return this;
    }

    public T SetReplaceRelation<T>(string eventId) where T : TimelineEventContent => SetReplaceRelation(eventId) as T ?? throw new InvalidOperationException();

    public class MessageRelatesTo {
        [JsonPropertyName("m.in_reply_to")]
        public EventInReplyTo? InReplyTo { get; set; }

        [JsonPropertyName("event_id")]
        public string? EventId { get; set; }

        [JsonPropertyName("rel_type")]
        public string? RelationType { get; set; }

        // used for reactions
        [JsonPropertyName("key")]
        public string? Key { get; set; }

        public class EventInReplyTo {
            [JsonPropertyName("event_id")]
            public string? EventId { get; set; }

            [JsonPropertyName("rel_type")]
            public string? RelType { get; set; }
        }
    }
}