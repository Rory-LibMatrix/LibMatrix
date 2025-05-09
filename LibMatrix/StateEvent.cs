using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ArcaneLibs;
using ArcaneLibs.Attributes;
using ArcaneLibs.Extensions;
using LibMatrix.EventTypes;
using LibMatrix.Extensions;

namespace LibMatrix;

public class StateEvent {
    public static FrozenSet<Type> KnownStateEventTypes { get; } = ClassCollector<EventContent>.ResolveFromAllAccessibleAssemblies().ToFrozenSet();

    public static FrozenDictionary<string, Type> KnownStateEventTypesByName { get; } = KnownStateEventTypes.Aggregate(
        new Dictionary<string, Type>(),
        (dict, type) => {
            var attrs = type.GetCustomAttributes<MatrixEventAttribute>();
            foreach (var attr in attrs) {
                if (dict.TryGetValue(attr.EventName, out var existing))
                    Console.WriteLine($"Duplicate event type '{attr.EventName}' registered for types '{existing.Name}' and '{type.Name}'");
                dict[attr.EventName] = type;
            }

            return dict;
        }).OrderBy(x => x.Key).ToFrozenDictionary();

    public static Type GetStateEventType(string? type) =>
        string.IsNullOrWhiteSpace(type) ? typeof(UnknownEventContent) : KnownStateEventTypesByName.GetValueOrDefault(type) ?? typeof(UnknownEventContent);

    [JsonIgnore]
    public Type MappedType => GetStateEventType(Type);

    [JsonIgnore]
    public bool IsLegacyType => MappedType.GetCustomAttributes<MatrixEventAttribute>().FirstOrDefault(x => x.EventName == Type)?.Legacy ?? false;

    [JsonIgnore]
    public string FriendlyTypeName => MappedType.GetFriendlyNameOrNull() ?? Type;

    [JsonIgnore]
    public string FriendlyTypeNamePlural => MappedType.GetFriendlyNamePluralOrNull() ?? Type;

    private static readonly JsonSerializerOptions TypedContentSerializerOptions = new() {
        // We need these, NumberHandling covers other number types that we don't want to convert
        Converters = {
            new JsonFloatStringConverter(),
            new JsonDoubleStringConverter(),
            new JsonDecimalStringConverter()
        }
    };

    [JsonIgnore]
    [SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
    public EventContent? TypedContent {
        get {
            try {
                var mappedType = GetStateEventType(Type);
                if (mappedType == typeof(UnknownEventContent))
                    Console.WriteLine($"Warning: unknown event type '{Type}'");
                var deserialisedContent = (EventContent)RawContent.Deserialize(mappedType, TypedContentSerializerOptions)!;
                return deserialisedContent;
            }
            catch (JsonException e) {
                Console.WriteLine(e);
                Console.WriteLine("Content:\n" + (RawContent?.ToJson() ?? "null"));
            }

            return null;
        }
        set {
            if (value is null)
                RawContent?.Clear();
            else
                RawContent = JsonSerializer.Deserialize<JsonObject>(JsonSerializer.Serialize(value, value.GetType(),
                    new JsonSerializerOptions() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull }));
        }
    }

    public T? ContentAs<T>() {
        try {
            return RawContent.Deserialize<T>(TypedContentSerializerOptions)!;
        }
        catch (JsonException e) {
            Console.WriteLine(e);
            Console.WriteLine("Content:\n" + (RawContent?.ToJson() ?? "null"));
        }

        return default;
    }

    [JsonPropertyName("state_key")]
    public string? StateKey { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("replaces_state")]
    public string? ReplacesState { get; set; }

    [JsonPropertyName("content")]
    // [field: AllowNull, MaybeNull]
    [NotNull]
    public JsonObject? RawContent { get; set; }

    //debug
    [JsonIgnore]
    public string InternalSelfTypeName {
        get {
            var res = GetType().Name switch {
                "StateEvent`1" => "StateEvent",
                _ => GetType().Name
            };
            return res;
        }
    }

    [JsonIgnore]
    public string InternalContentTypeName => TypedContent?.GetType().Name ?? "null";

    public static bool TypeKeyPairMatches(StateEventResponse x, StateEventResponse y) => x.Type == y.Type && x.StateKey == y.StateKey;
    public static bool Equals(StateEventResponse x, StateEventResponse y) => x.Type == y.Type && x.StateKey == y.StateKey && x.RawContent.Equals(y.RawContent);
}

public class StateEventResponse : StateEvent {
    [JsonPropertyName("origin_server_ts")]
    public long? OriginServerTs { get; set; }

    [JsonPropertyName("room_id")]
    public string? RoomId { get; set; }

    [JsonPropertyName("sender")]
    public string? Sender { get; set; }

    [JsonPropertyName("unsigned")]
    public JsonObject? Unsigned { get; set; }

    [JsonPropertyName("event_id")]
    public string? EventId { get; set; }

    public class UnsignedData {
        [JsonPropertyName("age"), JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
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

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(ChunkedStateEventResponse))]
internal partial class ChunkedStateEventResponseSerializerContext : JsonSerializerContext;

public class EventList {
    public EventList() { }

    public EventList(List<StateEventResponse>? events) {
        Events = events;
    }

    [JsonPropertyName("events")]
    public List<StateEventResponse>? Events { get; set; } = new();
}

public class ChunkedStateEventResponse {
    [JsonPropertyName("chunk")]
    public List<StateEventResponse>? Chunk { get; set; } = new();
}

public class PaginatedChunkedStateEventResponse : ChunkedStateEventResponse {
    [JsonPropertyName("start")]
    public string? Start { get; set; }

    [JsonPropertyName("end")]
    public string? End { get; set; }
}

public class BatchedChunkedStateEventResponse : ChunkedStateEventResponse {
    [JsonPropertyName("next_batch")]
    public string? NextBatch { get; set; }

    [JsonPropertyName("prev_batch")]
    public string? PrevBatch { get; set; }
}

public class RecursedBatchedChunkedStateEventResponse : BatchedChunkedStateEventResponse {
    [JsonPropertyName("recursion_depth")]
    public int? RecursionDepth { get; set; }
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

/*
public class ForgivingObjectConverter<T> : JsonConverter<T> where T : new() {
    public override T? Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) {
        try {
            var text = JsonDocument.ParseValue(ref reader).RootElement.GetRawText();
            return JsonSerializer.Deserialize<T>(text, options);
        }
        catch (JsonException ex) {
            Console.WriteLine(ex);
            return null;
        }
    }

    public override bool CanConvert(Type typeToConvert) => true;

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        => JsonSerializer.Serialize<T>(writer, value, options);
}*/

#endregion