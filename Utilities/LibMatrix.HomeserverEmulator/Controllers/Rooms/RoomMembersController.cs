using LibMatrix.EventTypes.Spec.State;
using LibMatrix.EventTypes.Spec.State.RoomInfo;
using LibMatrix.HomeserverEmulator.Services;
using Microsoft.AspNetCore.Mvc;

namespace LibMatrix.HomeserverEmulator.Controllers.Rooms;

[ApiController]
[Route("/_matrix/client/{version}/rooms/{roomId}/")]
public class RoomMembersController(
    ILogger<RoomMembersController> logger,
    TokenService tokenService,
    UserStore userStore,
    RoomStore roomStore,
    PaginationTokenResolverService paginationTokenResolver) : ControllerBase {
    [HttpGet("members")]
    public async Task<List<StateEventResponse>> GetMembers(string roomId, string? at = null, string? membership = null, string? not_membership = null) {
        var token = tokenService.GetAccessTokenOrNull(HttpContext);
        if (token == null)
            throw new MatrixException() {
                ErrorCode = "M_MISSING_TOKEN",
                Error = "Missing token"
            };

        var user = await userStore.GetUserByToken(token);
        if (user == null)
            throw new MatrixException() {
                ErrorCode = "M_UNKNOWN_TOKEN",
                Error = "No such user"
            };

        var room = roomStore.GetRoomById(roomId);
        if (room == null)
            throw new MatrixException() {
                ErrorCode = "M_NOT_FOUND",
                Error = "Room not found"
            };

        var members = room.Timeline.Where(x => x.Type == "m.room.member" && x.StateKey != null).ToList();

        if (membership != null)
            members = members.Where(x => (x.TypedContent as RoomMemberEventContent)?.Membership == membership).ToList();

        if (not_membership != null)
            members = members.Where(x => (x.TypedContent as RoomMemberEventContent)?.Membership != not_membership).ToList();

        if (at != null) {
            StateEventResponse? evt = null;
            if (at.StartsWith('$'))
                evt = await paginationTokenResolver.ResolveTokenToEvent(at, room);

            if (evt is null) {
                var time = await paginationTokenResolver.ResolveTokenToTimestamp(at);
                evt = room.Timeline.LastOrDefault(x => x.OriginServerTs <= time);
                if (evt is null) {
                    logger.LogWarning("Sent empty list of members for room {roomId} at {at}, because there were no events at this time!", roomId, at);
                    return [];
                }
            }
            else if (!room.Timeline.Contains(evt)) {
                evt = room.Timeline.LastOrDefault(x => x.OriginServerTs <= evt.OriginServerTs);
                if (evt is null) {
                    logger.LogWarning("Sent empty list of members for room {roomId} at {at}, because there were no events at this time!", roomId, at);
                    return [];
                }
            }

            // evt = room.Timeline.FirstOrDefault(x => x.EventId == at);
            if (evt == null)
                throw new MatrixException() {
                    ErrorCode = "M_NOT_FOUND",
                    Error = "Event not found"
                };

            members = members.Where(x => x.OriginServerTs <= evt.OriginServerTs).ToList();
        }

        return members;
    }
}