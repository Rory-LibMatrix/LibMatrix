using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using ArcaneLibs;
using ArcaneLibs.Extensions;
using LibMatrix.EventTypes;
using LibMatrix.Helpers;
using LibMatrix.Interfaces;

namespace LibMatrix;

public class StateEvent {
    public static List<Type> KnownStateEventTypes { get; } = new ClassCollector<EventContent>().ResolveFromAllAccessibleAssemblies();

    public static readonly Dictionary<string, Type> KnownStateEventTypesByName = KnownStateEventTypes.Aggregate(
        new Dictionary<string, Type>(),
        (dict, type) => {
            var attrs = type.GetCustomAttributes<MatrixEventAttribute>();
            foreach (var attr in attrs) {
                dict[attr.EventName] = type;
            }

            return dict;
        });

    public static Type GetStateEventType(string type) {
        if (type == "m.receipt") {
            return typeof(Dictionary<string, JsonObject>);
        }

        // var eventType = KnownStateEventTypes.FirstOrDefault(x =>
        // x.GetCustomAttributes<MatrixEventAttribute>()?.Any(y => y.EventName == type) ?? false);
        var eventType = KnownStateEventTypesByName.GetValueOrDefault(type);

        return eventType ?? typeof(UnknownEventContent);
    }

    public EventContent TypedContent {
        get {
            if(Type == "m.receipt") {
                return null!;
            }
            try {
                return (EventContent)RawContent.Deserialize(GetType)!;
            }
            catch (JsonException e) {
                Console.WriteLine(e);
                Console.WriteLine("Content:\n" + (RawContent?.ToJson() ?? "null"));
            }

            return null;
        }
        set => RawContent = JsonSerializer.Deserialize<JsonObject>(JsonSerializer.Serialize(value, value.GetType()));
    }

    [JsonPropertyName("state_key")]
    public string StateKey { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("replaces_state")]
    public string? ReplacesState { get; set; }

    private JsonObject? _rawContent;

    [JsonPropertyName("content")]
    public JsonObject? RawContent {
        get => _rawContent;
        set {
            _rawContent = value;
            // if (Type is not null && this is StateEventResponse stateEventResponse) {
            //     if (File.Exists($"unknown_state_events/{Type}/{stateEventResponse.EventId}.json")) return;
            //     var x = GetType.Name;
            // }
        }
    }

    [JsonIgnore]
    public new Type GetType {
        get {
            var type = GetStateEventType(Type);

            //special handling for some types
            // if (type == typeof(RoomEmotesEventContent)) {
            //     RawContent["emote"] = RawContent["emote"]?.AsObject() ?? new JsonObject();
            // }
            //
            // if (this is StateEventResponse stateEventResponse) {
            //     if (type == null || type == typeof(object)) {
            //         Console.WriteLine($"Warning: unknown event type '{Type}'!");
            //         Console.WriteLine(RawContent.ToJson());
            //         Directory.CreateDirectory($"unknown_state_events/{Type}");
            //         File.WriteAllText($"unknown_state_events/{Type}/{stateEventResponse.EventId}.json",
            //             RawContent.ToJson());
            //         Console.WriteLine($"Saved to unknown_state_events/{Type}/{stateEventResponse.EventId}.json");
            //     }
            //     else if (RawContent is not null && RawContent.FindExtraJsonObjectFields(type)) {
            //         Directory.CreateDirectory($"unknown_state_events/{Type}");
            //         File.WriteAllText($"unknown_state_events/{Type}/{stateEventResponse.EventId}.json",
            //             RawContent.ToJson());
            //         Console.WriteLine($"Saved to unknown_state_events/{Type}/{stateEventResponse.EventId}.json");
            //     }
            // }

            return type;
        }
    }

    //debug
    [JsonIgnore]
    public string dtype {
        get {
            var res = GetType().Name switch {
                "StateEvent`1" => "StateEvent",
                _ => GetType().Name
            };
            return res;
        }
    }

    [JsonIgnore]
    public string cdtype => TypedContent.GetType().Name;
}

public class StateEventResponse : StateEvent {
    [JsonPropertyName("origin_server_ts")]
    public ulong OriginServerTs { get; set; }

    [JsonPropertyName("room_id")]
    public string RoomId { get; set; }

    [JsonPropertyName("sender")]
    public string Sender { get; set; }

    [JsonPropertyName("unsigned")]
    public UnsignedData? Unsigned { get; set; }

    [JsonPropertyName("event_id")]
    public string EventId { get; set; }

    [JsonPropertyName("user_id")]
    public string UserId { get; set; }

    [JsonPropertyName("replaces_state")]
    public new string ReplacesState { get; set; }

    public class UnsignedData {
        [JsonPropertyName("age")]
        public ulong? Age { get; set; }

        [JsonPropertyName("redacted_because")]
        public object? RedactedBecause { get; set; }

        [JsonPropertyName("transaction_id")]
        public string? TransactionId { get; set; }

        [JsonPropertyName("replaces_state")]
        public string? ReplacesState { get; set; }

        [JsonPropertyName("prev_sender")]
        public string? PrevSender { get; set; }

        [JsonPropertyName("prev_content")]
        public JsonObject? PrevContent { get; set; }
    }
}

public class EventList {
    [JsonPropertyName("events")]
    public List<StateEventResponse>? Events { get; set; } = new();
}

public class ChunkedStateEventResponse {
    [JsonPropertyName("chunk")]
    public List<StateEventResponse>? Chunk { get; set; } = new();
}

#region Unused code

/*
public class StateEventContentPolymorphicTypeInfoResolver : DefaultJsonTypeInfoResolver
{
    public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        JsonTypeInfo jsonTypeInfo = base.GetTypeInfo(type, options);

        Type baseType = typeof(EventContent);
        if (jsonTypeInfo.Type == baseType) {
            jsonTypeInfo.PolymorphismOptions = new JsonPolymorphismOptions {
                TypeDiscriminatorPropertyName = "type",
                IgnoreUnrecognizedTypeDiscriminators = true,
                UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType,

                DerivedTypes = StateEvent.KnownStateEventTypesByName.Select(x => new JsonDerivedType(x.Value, x.Key)).ToList()

                // DerivedTypes = new ClassCollector<EventContent>()
                // .ResolveFromAllAccessibleAssemblies()
                // .SelectMany(t => t.GetCustomAttributes<MatrixEventAttribute>()
                // .Select(a => new JsonDerivedType(t, attr.EventName));

            };
        }

        return jsonTypeInfo;
    }
}
*/

#endregion
