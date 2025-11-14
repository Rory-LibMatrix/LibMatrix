using System.Text.Json.Serialization;
using LibMatrix.HomeserverEmulator.Services;
using Microsoft.AspNetCore.Mvc;

namespace LibMatrix.HomeserverEmulator.Controllers.Rooms;

[ApiController]
[Route("/_matrix/client/{version}/rooms/{roomId}")]
public class RoomAccountDataController(ILogger<RoomAccountDataController> logger, TokenService tokenService, UserStore userStore, RoomStore roomStore) : ControllerBase {
    [HttpPost("read_markers")]
    public async Task<object> SetReadMarkers(string roomId, [FromBody] ReadMarkersData data) {
        var token = tokenService.GetAccessToken(HttpContext);
        var user = await userStore.GetUserByToken(token);

        var room = roomStore.GetRoomById(roomId);
        if (room == null)
            throw new MatrixException() {
                ErrorCode = "M_NOT_FOUND",
                Error = "Room not found"
            };

        if (!room.JoinedMembers.Any(x => x.StateKey == user.UserId))
            throw new MatrixException() {
                ErrorCode = "M_FORBIDDEN",
                Error = "User is not in the room"
            };

        if (!room.ReadMarkers.ContainsKey(user.UserId))
            room.ReadMarkers[user.UserId] = new();

        if (data.FullyRead != null)
            room.ReadMarkers[user.UserId].FullyRead = data.FullyRead;
        if (data.Read != null)
            room.ReadMarkers[user.UserId].Read = data.Read;
        if (data.ReadPrivate != null)
            room.ReadMarkers[user.UserId].ReadPrivate = data.ReadPrivate;

        if (!room.AccountData.ContainsKey(user.UserId))
            room.AccountData[user.UserId] = new();

        room.AccountData[user.UserId].Add(new MatrixEventResponse() {
            Type = "m.fully_read",
            StateKey = user.UserId,
            RawContent = new() {
                ["event_id"] = data.FullyRead
            }
        });

        room.AccountData[user.UserId].Add(new MatrixEventResponse() {
            Type = "m.read",
            StateKey = user.UserId,
            RawContent = new() {
                ["event_id"] = data.Read
            }
        });

        room.AccountData[user.UserId].Add(new MatrixEventResponse() {
            Type = "m.read.private",
            StateKey = user.UserId,
            RawContent = new() {
                ["event_id"] = data.ReadPrivate
            }
        });

        return data;
    }
}

public class ReadMarkersData {
    [JsonPropertyName("m.fully_read")]
    public string? FullyRead { get; set; }

    [JsonPropertyName("m.read")]
    public string? Read { get; set; }

    [JsonPropertyName("m.read.private")]
    public string? ReadPrivate { get; set; }
}