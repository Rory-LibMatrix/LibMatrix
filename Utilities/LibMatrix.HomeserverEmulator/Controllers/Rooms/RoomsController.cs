using System.Text.Json.Serialization;
using LibMatrix.EventTypes.Spec.State;
using LibMatrix.EventTypes.Spec.State.RoomInfo;
using LibMatrix.HomeserverEmulator.Services;
using LibMatrix.Responses;
using LibMatrix.RoomTypes;
using Microsoft.AspNetCore.Mvc;

namespace LibMatrix.HomeserverEmulator.Controllers.Rooms;

[ApiController]
[Route("/_matrix/client/{version}/")]
public class RoomsController(ILogger<RoomsController> logger, TokenService tokenService, UserStore userStore, RoomStore roomStore) : ControllerBase {
    //createRoom
    [HttpPost("createRoom")]
    public async Task<RoomIdResponse> CreateRoom([FromBody] CreateRoomRequest request) {
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

        // var room = new RoomStore.Room($"!{Guid.NewGuid()}:{tokenService.GenerateServerName(HttpContext)}");
        var room = roomStore.CreateRoom(request, user);

        return new() {
            RoomId = room.RoomId
        };
    }

    [HttpPost("rooms/{roomId}/upgrade")]
    public async Task<object> UpgradeRoom(string roomId, [FromBody] UpgradeRoomRequest request) {
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

        var oldRoom = roomStore.GetRoomById(roomId);
        if (oldRoom == null)
            throw new MatrixException() {
                ErrorCode = "M_NOT_FOUND",
                Error = "Room not found"
            };

        var room = new RoomStore.Room($"!{Guid.NewGuid()}:{tokenService.GenerateServerName(HttpContext)}");

        var eventTypesToTransfer = new[] {
            RoomServerAclEventContent.EventId,
            RoomEncryptionEventContent.EventId,
            RoomNameEventContent.EventId,
            RoomAvatarEventContent.EventId,
            RoomTopicEventContent.EventId,
            RoomGuestAccessEventContent.EventId,
            RoomHistoryVisibilityEventContent.EventId,
            RoomJoinRulesEventContent.EventId,
            RoomPowerLevelEventContent.EventId,
        };

        var createEvent = room.SetStateInternal(new() {
            Type = RoomCreateEventContent.EventId,
            RawContent = new() {
                ["creator"] = user.UserId
            }
        });

        oldRoom.State.Where(x => eventTypesToTransfer.Contains(x.Type)).ToList().ForEach(x => room.SetStateInternal(x));

        room.AddUser(user.UserId);

        // user.Rooms.Add(room.RoomId, room);
        return new {
            replacement_room = room.RoomId
        };
    }

    public class ReasonBody {
        [JsonPropertyName("reason")]
        public string? Reason { get; set; }
    }
    [HttpPost("rooms/{roomId}/leave")] // TODO: implement
    public async Task<object> LeaveRoom(string roomId, [FromBody] ReasonBody body) {
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
        
        room.SetStateInternal(new() {
            Type = RoomMemberEventContent.EventId,
            TypedContent = new RoomMemberEventContent() {
                Membership = "leave",
                Reason = body.Reason
            },
            StateKey = user.UserId
        });

        logger.LogTrace($"User {user.UserId} left room {room.RoomId}");
        return new {
            room_id = room.RoomId
        };
    }
}

public class UpgradeRoomRequest {
    [JsonPropertyName("new_version")]
    public required string NewVersion { get; set; }
}