using System.Runtime.Intrinsics.X86;
using LibMatrix.EventTypes.Spec.State.RoomInfo;
using LibMatrix.Homeservers;
using LibMatrix.Responses;
using LibMatrix.RoomTypes;

namespace LibMatrix.Helpers;

public class RoomBuilder {
    private static readonly string[] V12PlusRoomVersions = ["org.matrix.hydra.11", "12"];
    public string? Type { get; set; }
    public string Version { get; set; } = "11";
    public RoomNameEventContent Name { get; set; } = new();
    public RoomTopicEventContent Topic { get; set; } = new();
    public RoomAvatarEventContent Avatar { get; set; } = new();
    public RoomCanonicalAliasEventContent CanonicalAlias { get; set; } = new();
    public string AliasLocalPart { get; set; } = string.Empty;
    public bool IsFederatable { get; set; } = true;
    public long OwnPowerLevel { get; set; } = MatrixConstants.MaxSafeJsonInteger;

    public RoomJoinRulesEventContent JoinRules { get; set; } = new() {
        JoinRule = RoomJoinRulesEventContent.JoinRules.Public
    };

    public RoomHistoryVisibilityEventContent HistoryVisibility { get; set; } = new() {
        HistoryVisibility = RoomHistoryVisibilityEventContent.HistoryVisibilityTypes.Shared
    };

    public RoomGuestAccessEventContent GuestAccess { get; set; } = new() {
        GuestAccess = "forbidden"
    };

    public RoomServerAclEventContent ServerAcls { get; set; } = new() {
        AllowIpLiterals = false
    };

    /// <summary>
    ///   State events to be sent *before* room access is configured. Keep this small!
    /// </summary>
    public List<StateEvent> ImportantState { get; set; } = [];

    /// <summary>
    ///   State events to be sent *after* room access is configured, but before invites are sent.
    /// </summary>
    public List<StateEvent> InitialState { get; set; } = [];

    /// <summary>
    ///   Users to invite, with optional reason
    /// </summary>
    public Dictionary<string, string?> Invites { get; set; } = new();

    public RoomPowerLevelEventContent PowerLevels { get; set; } = new() {
        EventsDefault = 0,
        UsersDefault = 0,
        Kick = 50,
        Invite = 50,
        Ban = 50,
        Redact = 50,
        StateDefault = 50,
        NotificationsPl = new() {
            Room = 50
        },
        Users = [],
        Events = new Dictionary<string, long> {
            { RoomAvatarEventContent.EventId, 50 },
            { RoomCanonicalAliasEventContent.EventId, 50 },
            { RoomEncryptionEventContent.EventId, 100 },
            { RoomHistoryVisibilityEventContent.EventId, 100 },
            { RoomGuestAccessEventContent.EventId, 100 },
            { RoomNameEventContent.EventId, 50 },
            { RoomPowerLevelEventContent.EventId, 100 },
            { RoomServerAclEventContent.EventId, 100 },
            { RoomTombstoneEventContent.EventId, 150 },
            { RoomPolicyServerEventContent.EventId, 100 }
        }
    };

    public Dictionary<string, object> AdditionalCreationContent { get; set; } = new();
    public List<string> AdditionalCreators { get; set; } = new();

    public async Task<GenericRoom> Create(AuthenticatedHomeserverGeneric homeserver) {
        var crq = new CreateRoomRequest() {
            PowerLevelContentOverride = new() {
                EventsDefault = 1000000,
                UsersDefault = 1000000,
                Kick = 1000000,
                Invite = 1000000,
                Ban = 1000000,
                Redact = 1000000,
                StateDefault = 1000000,
                NotificationsPl = new() {
                    Room = 1000000
                },
                Users = V12PlusRoomVersions.Contains(Version)
                    ? []
                    : new() {
                        { homeserver.WhoAmI.UserId, MatrixConstants.MaxSafeJsonInteger }
                    },
                Events = new Dictionary<string, long> {
                    { RoomAvatarEventContent.EventId, 1000000 },
                    { RoomCanonicalAliasEventContent.EventId, 1000000 },
                    { RoomEncryptionEventContent.EventId, 1000000 },
                    { RoomHistoryVisibilityEventContent.EventId, 1000000 },
                    { RoomGuestAccessEventContent.EventId, 1000000 },
                    { RoomNameEventContent.EventId, 1000000 },
                    { RoomPowerLevelEventContent.EventId, 1000000 },
                    { RoomServerAclEventContent.EventId, 1000000 },
                    { RoomTombstoneEventContent.EventId, 1000000 },
                    { RoomPolicyServerEventContent.EventId, 1000000 }
                },
            },
            Visibility = "private",
            RoomVersion = Version
        };

        if (!string.IsNullOrWhiteSpace(Type))
            crq.CreationContent.Add("type", Type);

        if (!IsFederatable)
            crq.CreationContent.Add("m.federate", false);

        AdditionalCreators.RemoveAll(string.IsNullOrWhiteSpace);
        if (V12PlusRoomVersions.Contains(Version) && AdditionalCreators is { Count: > 0 }) {
            crq.CreationContent.Add("additional_creators", AdditionalCreators);
        }

        foreach (var kvp in AdditionalCreationContent) {
            crq.CreationContent.Add(kvp.Key, kvp.Value);
        }

        var room = await homeserver.CreateRoom(crq);

        await SetBasicRoomInfoAsync(room);
        await SetStatesAsync(room, ImportantState);
        await SetAccessAsync(room);
        await SetStatesAsync(room, InitialState);
        await SendInvites(room);

        return room;
    }

    private async Task SendInvites(GenericRoom room) {
        if (Invites.Count == 0) return;

        var inviteTasks = Invites.Select(async kvp => {
            try {
                await room.InviteUserAsync(kvp.Key, kvp.Value);
            }
            catch (MatrixException e) {
                Console.Error.WriteLine("Failed to invite {0} to {1}: {2}", kvp.Key, room.RoomId, e.Message);
            }
        });

        await Task.WhenAll(inviteTasks);
    }

    private async Task SetStatesAsync(GenericRoom room, List<StateEvent> state) {
        foreach (var ev in state) {
            await (string.IsNullOrWhiteSpace(ev.StateKey)
                ? room.SendStateEventAsync(ev.Type, ev.RawContent)
                : room.SendStateEventAsync(ev.Type, ev.StateKey, ev.RawContent));
        }
    }

    private async Task SetBasicRoomInfoAsync(GenericRoom room) {
        if (!string.IsNullOrWhiteSpace(Name.Name))
            await room.SendStateEventAsync(RoomNameEventContent.EventId, Name);

        if (!string.IsNullOrWhiteSpace(Topic.Topic))
            await room.SendStateEventAsync(RoomTopicEventContent.EventId, Topic);

        if (!string.IsNullOrWhiteSpace(Avatar.Url))
            await room.SendStateEventAsync(RoomAvatarEventContent.EventId, Avatar);

        if (!string.IsNullOrWhiteSpace(AliasLocalPart))
            CanonicalAlias.Alias = $"#{AliasLocalPart}:{room.Homeserver.ServerName}";

        if (!string.IsNullOrWhiteSpace(CanonicalAlias.Alias)) {
            await room.Homeserver.SetRoomAliasAsync(CanonicalAlias.Alias!, room.RoomId);
            await room.SendStateEventAsync(RoomCanonicalAliasEventContent.EventId, CanonicalAlias);
        }
    }

    private async Task SetAccessAsync(GenericRoom room) {
        PowerLevels.Users![room.Homeserver.WhoAmI.UserId] = OwnPowerLevel;
        await room.SendStateEventAsync(RoomPowerLevelEventContent.EventId, PowerLevels);

        if (!string.IsNullOrWhiteSpace(HistoryVisibility.HistoryVisibility))
            await room.SendStateEventAsync(RoomHistoryVisibilityEventContent.EventId, HistoryVisibility);

        if (!string.IsNullOrWhiteSpace(JoinRules.JoinRuleValue))
            await room.SendStateEventAsync(RoomJoinRulesEventContent.EventId, JoinRules);
    }
}

public class MatrixConstants {
    public const long MaxSafeJsonInteger = 9007199254740991L; // 2^53 - 1
}