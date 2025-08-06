using System.Text.Json.Serialization;
using LibMatrix.EventTypes.Spec;
using LibMatrix.EventTypes.Spec.State.RoomInfo;
using LibMatrix.RoomTypes;

namespace LibMatrix.Helpers;

public class RoomUpgradeBuilder(GenericRoom oldRoom) : RoomBuilder {
    public GenericRoom OldRoom { get; } = oldRoom;
    public RoomUpgradeOptions UpgradeOptions { get; set; } = new();

    public async Task ImportAsync() {
        await foreach (var evt in OldRoom.GetFullStateAsync()) {
            if (evt is null) continue;
            if (evt.StateKey == "") {
                if (evt.TypedContent is RoomCreateEventContent createEvt)
                    foreach (var (key, value) in evt.RawContent) {
                        if (key == "version") continue;
                        if (key == "type")
                            Type = value!.GetValue<string>();
                        else AdditionalCreationContent[key] = value;
                    }
                else if (evt.TypedContent is RoomNameEventContent name)
                    Name = name;
                else if (evt.TypedContent is RoomTopicEventContent topic)
                    Topic = topic;
                else if (evt.TypedContent is RoomAvatarEventContent avatar)
                    Avatar = avatar;
                else if (evt.TypedContent is RoomCanonicalAliasEventContent alias) {
                    CanonicalAlias = alias;
                    AliasLocalPart = alias.Alias?.Split(':',2).FirstOrDefault()?[1..] ?? string.Empty;
                }
                else if (evt.TypedContent is RoomJoinRulesEventContent joinRules)
                    JoinRules = joinRules;
                else if (evt.TypedContent is RoomHistoryVisibilityEventContent historyVisibility)
                    HistoryVisibility = historyVisibility;
                else if (evt.TypedContent is RoomGuestAccessEventContent guestAccess)
                    GuestAccess = guestAccess;
                else if (evt.TypedContent is RoomServerAclEventContent serverAcls)
                    ServerAcls = serverAcls;
                else if (evt.TypedContent is RoomPowerLevelEventContent powerLevels) {
                    if (UpgradeOptions.InvitePowerlevelUsers && powerLevels.Users != null)
                        foreach (var (userId, level) in powerLevels.Users)
                            if (level > powerLevels.UsersDefault)
                                Invites.Add(userId, "Room upgrade (had a power level)");

                    PowerLevels = powerLevels;
                }
                else if (evt.TypedContent is RoomEncryptionEventContent encryption)
                    Encryption = encryption;
                else if (evt.TypedContent is RoomPinnedEventContent) ; // Discard as you can't cross reference pinned events
                else InitialState.Add(evt);
            }
            else if (evt.Type == RoomMemberEventContent.EventId) {
                if (UpgradeOptions.InviteMembers && evt.TypedContent is RoomMemberEventContent { Membership: "join" or "invite" })
                    if (!Invites.ContainsKey(evt.StateKey))
                        Invites.Add(evt.StateKey, "Room upgrade");
                    else if (UpgradeOptions.MigrateBans && evt.TypedContent is RoomMemberEventContent { Membership: "ban" } bannedMember)
                        Bans.Add(evt.StateKey, bannedMember.Reason);
            }
            else InitialState.Add(evt);
        }
    }

    public class RoomUpgradeOptions {
        public bool InviteMembers { get; set; }
        public bool InvitePowerlevelUsers { get; set; }
        public bool MigrateBans { get; set; }

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
}