using System.Diagnostics;
using System.Text.Json.Serialization;
using ArcaneLibs;
using LibMatrix.EventTypes.Spec;
using LibMatrix.EventTypes.Spec.State.Policy;
using LibMatrix.EventTypes.Spec.State.RoomInfo;
using LibMatrix.Homeservers;
using LibMatrix.RoomTypes;

namespace LibMatrix.Helpers;

public class RoomUpgradeBuilder : RoomBuilder {
    public RoomUpgradeOptions UpgradeOptions { get; set; } = new();
    public string OldRoomId { get; set; } = string.Empty;
    public bool CanUpgrade { get; private set; }
    public Dictionary<string, object> AdditionalTombstoneContent { get; set; } = new();

    public async Task ImportAsync(GenericRoom OldRoom) {
        var sw = Stopwatch.StartNew();
        var total = 0;

        var basePolicyTypes = ClassCollector<PolicyRuleEventContent>.ResolveFromAllAccessibleAssemblies().ToList();
        Console.WriteLine($"Found {basePolicyTypes.Count} policy types in {sw.ElapsedMilliseconds}ms");
        CanUpgrade = (
                         (await OldRoom.GetPowerLevelsAsync())?.UserHasStatePermission(OldRoom.Homeserver.UserId, RoomTombstoneEventContent.EventId)
                         ?? (await OldRoom.GetRoomCreatorsAsync()).Contains(OldRoom.Homeserver.UserId)
                     )
                     || (OldRoom.IsV12PlusRoomId && (await OldRoom.GetRoomCreatorsAsync()).Contains(OldRoom.Homeserver.UserId));

        await foreach (var srcEvt in OldRoom.GetFullStateAsync()) {
            total++;
            if (srcEvt is null) continue;
            var evt = srcEvt;

            if (UpgradeOptions.UpgradeUnstableValues) {
                evt = UpgradeUnstableValues(evt);
            }

            if (evt.StateKey == "") {
                if (evt.Type == RoomCreateEventContent.EventId)
                    foreach (var (key, value) in evt.RawContent) {
                        if (key == "version") continue;
                        if (key == "type")
                            Type = value!.GetValue<string>();
                        else AdditionalCreationContent[key] = value;
                    }
                else if (evt.Type == RoomNameEventContent.EventId)
                    Name = evt.ContentAs<RoomNameEventContent>()!;
                else if (evt.Type == RoomTopicEventContent.EventId)
                    Topic = evt.ContentAs<RoomTopicEventContent>()!;
                else if (evt.Type == RoomAvatarEventContent.EventId)
                    Avatar = evt.ContentAs<RoomAvatarEventContent>()!;
                else if (evt.Type == RoomCanonicalAliasEventContent.EventId) {
                    CanonicalAlias = evt.ContentAs<RoomCanonicalAliasEventContent>()!;
                    AliasLocalPart = CanonicalAlias.Alias?.Split(':', 2).FirstOrDefault()?[1..] ?? string.Empty;
                }
                else if (evt.Type == RoomJoinRulesEventContent.EventId)
                    JoinRules = evt.ContentAs<RoomJoinRulesEventContent>()!;
                else if (evt.Type == RoomHistoryVisibilityEventContent.EventId)
                    HistoryVisibility = evt.ContentAs<RoomHistoryVisibilityEventContent>()!;
                else if (evt.Type == RoomGuestAccessEventContent.EventId)
                    GuestAccess = evt.ContentAs<RoomGuestAccessEventContent>()!;
                else if (evt.Type == RoomServerAclEventContent.EventId)
                    ServerAcls = evt.ContentAs<RoomServerAclEventContent>()!;
                else if (evt.Type == RoomPowerLevelEventContent.EventId) {
                    PowerLevels = evt.ContentAs<RoomPowerLevelEventContent>()!;
                    if (UpgradeOptions.InvitePowerlevelUsers && PowerLevels.Users != null)
                        foreach (var (userId, level) in PowerLevels.Users)
                            if (level > PowerLevels.UsersDefault)
                                Invites.Add(userId, "Room upgrade (had a power level)");
                }
                else if (evt.Type == RoomEncryptionEventContent.EventId)
                    Encryption = evt.ContentAs<RoomEncryptionEventContent>();
                else if (evt.Type == RoomPinnedEventContent.EventId) ; // Discard as you can't cross reference pinned events
                else
                    InitialState.Add(new() {
                        Type = evt.Type,
                        StateKey = evt.StateKey,
                        RawContent = evt.RawContent
                    });
            }
            else if (evt.Type == RoomMemberEventContent.EventId) {
                if (UpgradeOptions.InviteMembers && evt.TypedContent is RoomMemberEventContent { Membership: "join" or "invite" } invitedMember) {
                    Invites.TryAdd(evt.StateKey!, invitedMember.Reason ?? "Room upgrade");
                }
                else if (UpgradeOptions.MigrateBans && evt.TypedContent is RoomMemberEventContent { Membership: "ban" } bannedMember)
                    Bans.TryAdd(evt.StateKey!, bannedMember.Reason);
            }
            else if (!UpgradeOptions.MigrateEmptyStateEvents && evt.RawContent.Count == 0) { } // skip empty state events
            else if (basePolicyTypes.Contains(evt.MappedType)) ImportPolicyEventAsync(evt);
            else
                InitialState.Add(new() {
                    Type = evt.Type,
                    StateKey = evt.StateKey,
                    RawContent = evt.RawContent
                });
        }

        Console.WriteLine($"Imported {total} state events from old room {OldRoom.RoomId} in {sw.ElapsedMilliseconds}ms");
    }

    private StateEventResponse UpgradeUnstableValues(StateEventResponse evt) {
        
        return evt;
    }

    private void ImportPolicyEventAsync(StateEventResponse evt) {
        var msc4321Options = UpgradeOptions.Msc4321PolicyListUpgradeOptions;
        if (msc4321Options is { Enable: true, UpgradeType: Msc4321PolicyListUpgradeOptions.Msc4321PolicyListUpgradeType.Transition })
            return; // this upgrade type doesnt copy policies
        if (msc4321Options.Enable) {
            evt.RawContent["org.matrix.msc4321.original_sender"] = evt.Sender;
            evt.RawContent["org.matrix.msc4321.original_timestamp"] = evt.OriginServerTs;
            evt.RawContent["org.matrix.msc4321.original_event_id"] = evt.EventId;
        }
        InitialState.Add(new() {
            Type = evt.Type,
            StateKey = evt.StateKey,
            RawContent = evt.RawContent
        });
    }

    public override async Task<GenericRoom> Create(AuthenticatedHomeserverGeneric homeserver) {
        var room = await base.Create(homeserver);
        if (CanUpgrade || UpgradeOptions.ForceUpgrade) {
            if (UpgradeOptions.RoomUpgradeNotice != null) {
                var noticeContent = await UpgradeOptions.RoomUpgradeNotice(room);
                await room.SendMessageEventAsync(noticeContent);
            }

            var tombstoneContent = new RoomTombstoneEventContent {
                Body = "This room has been upgraded to a new version.",
                ReplacementRoom = room.RoomId
            };

            tombstoneContent.AdditionalData ??= [];
            foreach (var (key, value) in AdditionalTombstoneContent)
                tombstoneContent.AdditionalData[key] = value;

            await room.SendStateEventAsync(RoomTombstoneEventContent.EventId, tombstoneContent);
        }
        return room;
    }

    public class RoomUpgradeOptions {
        public bool InviteMembers { get; set; }
        public bool InvitePowerlevelUsers { get; set; }
        public bool MigrateBans { get; set; }
        public bool MigrateEmptyStateEvents { get; set; }
        public bool UpgradeUnstableValues { get; set; }
        public bool ForceUpgrade { get; set; }
        public Msc4321PolicyListUpgradeOptions Msc4321PolicyListUpgradeOptions { get; set; } = new();

        [JsonIgnore]
        public Func<GenericRoom, Task<RoomMessageEventContent>> RoomUpgradeNotice { get; set; } = async newRoom => new MessageBuilder()
            .WithRoomMention()
            .WithBody("This room has been upgraded to a new version. This version of the room will be kept as an archive.")
            .WithNewline()
            .WithBody("You can join the new room by clicking the link below:")
            .WithNewline()
            .WithRoomMention()
            .WithMention(newRoom.RoomId, await newRoom.GetNameOrFallbackAsync(), vias: (await newRoom.GetHomeserversInRoom()).ToArray(), useLinkInPlainText: true)
            .Build();
    }

    public class Msc4321PolicyListUpgradeOptions {
        public bool Enable { get; set; } = true;
        public Msc4321PolicyListUpgradeType UpgradeType { get; set; } = Msc4321PolicyListUpgradeType.Move;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public enum Msc4321PolicyListUpgradeType {
            /// <summary>
            ///     Copy policies, unwatch old list 
            /// </summary>
            Move,

            /// <summary>
            ///     Don't copy policies
            /// </summary>
            Transition
        }
    }
}