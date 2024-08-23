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

namespace LibMatrix.Homeservers.ImplementationDetails.Synapse.Models.Responses;

public class SynapseAdminEventReportListResult : SynapseNextTokenTotalCollectionResult {
    [JsonPropertyName("event_reports")]
    public List<SynapseAdminEventReportListResultReport> Reports { get; set; } = new();

    public class SynapseAdminEventReportListResultReport {
        [JsonPropertyName("event_id")]
        public string EventId { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }

        [JsonPropertyName("score")]
        public int? Score { get; set; }

        [JsonPropertyName("received_ts")]
        public long ReceivedTs { get; set; }

        [JsonPropertyName("canonical_alias")]
        public string? CanonicalAlias { get; set; }

        [JsonPropertyName("room_id")]
        public string RoomId { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("sender")]
        public string Sender { get; set; }

        [JsonPropertyName("user_id")]
        public string UserId { get; set; }

        [JsonIgnore]
        public DateTime ReceivedTsDateTime {
            get => DateTimeOffset.FromUnixTimeMilliseconds(ReceivedTs).DateTime;
            set => ReceivedTs = new DateTimeOffset(value).ToUnixTimeMilliseconds();
        }
    }

    public class SynapseAdminEventReportListResultReportWithDetails : SynapseAdminEventReportListResultReport {
        [JsonPropertyName("event_json")]
        public SynapseEventJson EventJson { get; set; }

        public class SynapseEventJson {
            [JsonPropertyName("auth_events")]
            public List<string> AuthEvents { get; set; }

            [JsonPropertyName("content")]
            public JsonObject? RawContent { get; set; }

            [JsonPropertyName("depth")]
            public int Depth { get; set; }

            [JsonPropertyName("hashes")]
            public Dictionary<string, string> Hashes { get; set; }

            [JsonPropertyName("origin")]
            public string Origin { get; set; }

            [JsonPropertyName("origin_server_ts")]
            public long OriginServerTs { get; set; }

            [JsonPropertyName("prev_events")]
            public List<string> PrevEvents { get; set; }

            [JsonPropertyName("prev_state")]
            public List<object> PrevState { get; set; }

            [JsonPropertyName("room_id")]
            public string RoomId { get; set; }

            [JsonPropertyName("sender")]
            public string Sender { get; set; }

            [JsonPropertyName("signatures")]
            public Dictionary<string, Dictionary<string, string>> Signatures { get; set; }

            [JsonPropertyName("type")]
            public string Type { get; set; }

            [JsonPropertyName("unsigned")]
            public JsonObject? Unsigned { get; set; }

            // Extra... copied from StateEventResponse

            [JsonIgnore]
            public Type MappedType => StateEvent.GetStateEventType(Type);

            [JsonIgnore]
            public bool IsLegacyType => MappedType.GetCustomAttributes<MatrixEventAttribute>().FirstOrDefault(x => x.EventName == Type)?.Legacy ?? false;

            [JsonIgnore]
            public string FriendlyTypeName => MappedType.GetFriendlyNameOrNull() ?? Type;

            [JsonIgnore]
            public string FriendlyTypeNamePlural => MappedType.GetFriendlyNamePluralOrNull() ?? Type;

            private static readonly JsonSerializerOptions TypedContentSerializerOptions = new() {
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
                    ClassCollector<EventContent>.ResolveFromAllAccessibleAssemblies();
                    // if (Type == "m.receipt") {
                    // return null;
                    // }
                    try {
                        var mappedType = StateEvent.GetStateEventType(Type);
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
        }
    }
}