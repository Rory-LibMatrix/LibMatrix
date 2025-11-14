using LibMatrix.EventTypes.Spec.State.RoomInfo;
using LibMatrix.Homeservers;
using LibMatrix.Responses;

namespace LibMatrix.Utilities.Bot.Interfaces;

public class RoomInviteContext {
    public required string RoomId { get; init; }
    public required AuthenticatedHomeserverGeneric Homeserver { get; init; }
    public required MatrixEventResponse MemberEvent { get; init; }
    public required SyncResponse.RoomsDataStructure.InvitedRoomDataStructure InviteData { get; init; }

    public async Task<string> TryGetInviterNameAsync() {
        var name = InviteData.InviteState?.Events?
            .FirstOrDefault(evt => evt is { Type: RoomMemberEventContent.EventId } && evt.StateKey == MemberEvent.Sender)?
            .ContentAs<RoomMemberEventContent>()?.DisplayName;

        if (!string.IsNullOrWhiteSpace(name))
            return name;

        try {
            await Homeserver.GetProfileAsync(MemberEvent.Sender!);
        }
        catch {
            //ignored
        }

        return MemberEvent.Sender!;
    }

    public async Task<string> TryGetRoomNameAsync() {
        // try to get room name from invite state
        var name = InviteData.InviteState?.Events?
            .FirstOrDefault(evt => evt is { Type: RoomNameEventContent.EventId, StateKey: "" })?
            .ContentAs<RoomNameEventContent>()?.Name;

        if (!string.IsNullOrWhiteSpace(name))
            return name;

        // try to get room alias
        var alias = InviteData.InviteState?.Events?
            .FirstOrDefault(evt => evt is { Type: RoomCanonicalAliasEventContent.EventId, StateKey: "" })?
            .ContentAs<RoomCanonicalAliasEventContent>()?.Alias;

        if (!string.IsNullOrWhiteSpace(alias))
            return alias;

        // try to get room name via public previews
        try {
            name = await Homeserver.GetRoom(RoomId).GetNameOrFallbackAsync();
            if (name != RoomId && !string.IsNullOrWhiteSpace(name))
                return name;
        }
        catch {
            //ignored
        }

        // fallback to room alias via public previews
        try {
            alias = (await Homeserver.GetRoom(RoomId).GetCanonicalAliasAsync())?.Alias;
            if (!string.IsNullOrWhiteSpace(alias))
                return alias;
        }
        catch {
            //ignored
        }

        // fall back to room ID
        return RoomId;
    }
}