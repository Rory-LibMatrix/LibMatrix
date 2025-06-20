using System.Text.Json.Serialization;
using LibMatrix.EventTypes.Spec.State.RoomInfo;
using LibMatrix.HomeserverEmulator.Services;
using LibMatrix.Responses;
using Microsoft.AspNetCore.Mvc;

namespace LibMatrix.HomeserverEmulator.Controllers;

[ApiController]
[Route("/_matrix/client/{version}/")]
public class UserController(ILogger<UserController> logger, TokenService tokenService, UserStore userStore, RoomStore roomStore) : ControllerBase {
    [HttpGet("account/whoami")]
    public async Task<WhoAmIResponse> Login() {
        var token = tokenService.GetAccessToken(HttpContext);
        var user = await userStore.GetUserByToken(token, Random.Shared.Next(101) <= 10, tokenService.GenerateServerName(HttpContext));
        if (user is null)
            throw new MatrixException() {
                ErrorCode = "M_UNKNOWN_TOKEN",
                Error = "Invalid token."
            };
        var whoAmIResponse = new WhoAmIResponse {
            UserId = user.UserId
        };
        return whoAmIResponse;
    }

    [HttpGet("joined_rooms")]
    public async Task<object> GetJoinedRooms() {
        var token = tokenService.GetAccessToken(HttpContext);
        var user = await userStore.GetUserByToken(token, false);
        if (user is null)
            throw new MatrixException() {
                ErrorCode = "M_UNAUTHORIZED",
                Error = "Invalid token."
            };
        // return user.JoinedRooms;

        return new {
            joined_rooms = roomStore._rooms.Where(r =>
                r.State.Any(s => s.StateKey == user.UserId && s.Type == RoomMemberEventContent.EventId && (s.TypedContent as RoomMemberEventContent).Membership == "join")
            ).Select(r => r.RoomId).ToList()
        };
    }
    
    [HttpGet("devices")]
    public async Task<DevicesResponse> GetDevices() {
        var token = tokenService.GetAccessToken(HttpContext);
        var user = await userStore.GetUserByToken(token, false);
        if (user is null)
            throw new MatrixException() {
                ErrorCode = "M_UNAUTHORIZED",
                Error = "Invalid token."
            };
        return new() {
            Devices = user.AccessTokens.Select(x=>new DevicesResponse.Device() {
                DeviceId = x.Value.DeviceId,
                DisplayName = x.Value.DeviceId
            }).ToList()
        };
    }

    public class DevicesResponse {
        [JsonPropertyName("devices")]
        public List<Device> Devices { get; set; }
        
        public class Device {
            [JsonPropertyName("device_id")]
            public string DeviceId { get; set; }
            [JsonPropertyName("display_name")]
            public string DisplayName { get; set; }
            [JsonPropertyName("last_seen_ip")]
            public string LastSeenIp { get; set; }
            [JsonPropertyName("last_seen_ts")]
            public long LastSeenTs { get; set; }
        }
    }
}