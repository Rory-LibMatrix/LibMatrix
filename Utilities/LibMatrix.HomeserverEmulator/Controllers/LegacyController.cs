using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using ArcaneLibs.Extensions;
using LibMatrix.EventTypes.Spec.State.RoomInfo;
using LibMatrix.HomeserverEmulator.Services;
using Microsoft.AspNetCore.Mvc;

namespace LibMatrix.HomeserverEmulator.Controllers;

[ApiController]
[Route("/_matrix/client/{version}/")]
public class LegacyController(ILogger<LegacyController> logger, TokenService tokenService, UserStore userStore, RoomStore roomStore) : ControllerBase {
    [HttpGet("rooms/{roomId}/initialSync")]
    [SuppressMessage("ReSharper.DPA", "DPA0011: High execution time of MVC action", Justification = "Endpoint is expected to wait until data is available or timeout.")]
    public async Task<object> Sync([FromRoute] string roomId, [FromQuery] int limit = 20) {
        var sw = Stopwatch.StartNew();
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
        if (room is null)
            throw new MatrixException() {
                ErrorCode = "M_NOT_FOUND",
                Error = "Room not found."
            };
        var accountData = room.AccountData.GetOrCreate(user.UserId, _ => []);
        var membership = room.State.FirstOrDefault(x => x.Type == "m.room.member" && x.StateKey == user.UserId);
        var timelineChunk = room.Timeline.TakeLast(limit).ToList();
        return new {
            account_data = accountData,
            membership = (membership?.TypedContent as RoomMemberEventContent)?.Membership ?? "leave",
            room_id = room.RoomId,
            state = room.State.ToList(),
            visibility = "public",
            messages = new PaginatedChunkedMatrixEventResponse() {
                Chunk = timelineChunk,
                End = timelineChunk.Last().EventId,
                Start = timelineChunk.Count >= limit ? timelineChunk.First().EventId : null
            }
        };
    }
}