using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using LibMatrix.EventTypes;
using LibMatrix.EventTypes.Spec.State;
using LibMatrix.EventTypes.Spec.State.RoomInfo;
using LibMatrix.Homeservers;

namespace LibMatrix.Responses;

public class CreateRoomRequest {
    [JsonIgnore] public CreationContentBaseType CreationContentBaseType;

    public CreateRoomRequest() {
        CreationContentBaseType = new CreationContentBaseType(this);
    }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Room alias local name. Must be unique on the homeserver.
    /// </summary>
    [JsonPropertyName("room_alias_name")]
    public string? RoomAliasName { get; set; }

    //we dont want to use this, we want more control
    // [JsonPropertyName("preset")]
    // public string Preset { get; set; } = null!;

    [JsonPropertyName("initial_state")]
    public List<StateEvent>? InitialState { get; set; }

    /// <summary>
    /// One of: ["public", "private"]
    /// </summary>
    [JsonPropertyName("visibility")]
    public string? Visibility { get; set; }

    [JsonPropertyName("power_level_content_override")]
    public RoomPowerLevelEventContent? PowerLevelContentOverride { get; set; } = null!;

    [JsonPropertyName("creation_content")]
    public JsonObject CreationContent { get; set; } = new();

    [JsonPropertyName("invite")]
    public List<string>? Invite { get; set; }

    /// <summary>
    ///     For use only when you can't use the CreationContent property
    /// </summary>

    public StateEvent this[string eventType, string eventKey = ""] {
        get {
            var stateEvent = InitialState.FirstOrDefault(x => x.Type == eventType && x.StateKey == eventKey);
            if (stateEvent == null)
                InitialState.Add(stateEvent = new StateEvent {
                    Type = eventType,
                    StateKey = eventKey,
                    TypedContent = (EventContent)Activator.CreateInstance(
                        StateEvent.KnownStateEventTypes.FirstOrDefault(x =>
                            x.GetCustomAttributes<MatrixEventAttribute>()?
                                .Any(y => y.EventName == eventType) ?? false) ?? typeof(UnknownEventContent)
                    )!
                });

            return stateEvent;
        }
        set {
            var stateEvent = InitialState.FirstOrDefault(x => x.Type == eventType && x.StateKey == eventKey);
            if (stateEvent == null)
                InitialState.Add(value);
            else
                InitialState[InitialState.IndexOf(stateEvent)] = value;
        }
    }

    public Dictionary<string, string> Validate() {
        Dictionary<string, string> errors = new();
        if (!Regex.IsMatch(RoomAliasName, @"[a-zA-Z0-9_\-]+$"))
            errors.Add("room_alias_name",
                "Room alias name must only contain letters, numbers, underscores, and hyphens.");

        return errors;
    }

    public static CreateRoomRequest CreatePublic(AuthenticatedHomeserverGeneric hs, string? name = null, string? roomAliasName = null) {
        var request = new CreateRoomRequest {
            Name = name ?? "New public Room",
            Visibility = "public",
            CreationContent = new JsonObject(),
            PowerLevelContentOverride = new RoomPowerLevelEventContent {
                EventsDefault = 0,
                UsersDefault = 0,
                Kick = 50,
                Ban = 50,
                Invite = 25,
                StateDefault = 10,
                Redact = 50,
                NotificationsPl = new RoomPowerLevelEventContent.NotificationsPL {
                    Room = 10
                },
                Events = new Dictionary<string, long> {
                    { "m.room.avatar", 50 },
                    { "m.room.canonical_alias", 50 },
                    { "m.room.encryption", 100 },
                    { "m.room.history_visibility", 100 },
                    { "m.room.name", 50 },
                    { "m.room.power_levels", 100 },
                    { "m.room.server_acl", 100 },
                    { "m.room.tombstone", 100 }
                },
                Users = new Dictionary<string, long> {
                    {
                        hs.UserId,
                        101
                    }
                }
            },
            RoomAliasName = roomAliasName,
            InitialState = new List<StateEvent>()
        };

        return request;
    }

    public static CreateRoomRequest CreatePrivate(AuthenticatedHomeserverGeneric hs, string? name = null, string? roomAliasName = null) {
        var request = new CreateRoomRequest {
            Name = name ?? "New private Room",
            Visibility = "private",
            CreationContent = new JsonObject(),
            PowerLevelContentOverride = new RoomPowerLevelEventContent {
                EventsDefault = 0,
                UsersDefault = 0,
                Kick = 50,
                Ban = 50,
                Invite = 25,
                StateDefault = 10,
                Redact = 50,
                NotificationsPl = new RoomPowerLevelEventContent.NotificationsPL {
                    Room = 10
                },
                Events = new Dictionary<string, long> {
                    { "m.room.avatar", 50 },
                    { "m.room.canonical_alias", 50 },
                    { "m.room.encryption", 100 },
                    { "m.room.history_visibility", 100 },
                    { "m.room.name", 50 },
                    { "m.room.power_levels", 100 },
                    { "m.room.server_acl", 100 },
                    { "m.room.tombstone", 100 }
                },
                Users = new Dictionary<string, long> {
                    {
                        hs.UserId,
                        101
                    }
                }
            },
            RoomAliasName = roomAliasName,
            InitialState = new List<StateEvent>()
        };

        return request;
    }
}