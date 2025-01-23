using System.Text.Json.Nodes;
using LibMatrix.HomeserverEmulator.Services;
using Microsoft.AspNetCore.Mvc;

namespace LibMatrix.HomeserverEmulator.Controllers;

[ApiController]
[Route("/_matrix/client/{version}/")]
public class ProfileController(ILogger<ProfileController> logger, TokenService tokenService, UserStore userStore, RoomStore roomStore) : ControllerBase {
    [HttpGet("profile/{userId}")]
    public async Task<IDictionary<string, object>> GetProfile(string userId) {
        var user = await userStore.GetUserById(userId, false);
        if (user is null)
            throw new MatrixException() {
                ErrorCode = "M_NOT_FOUND",
                Error = "User not found."
            };
        return user.Profile;
    }

    [HttpGet("profile/{userId}/{key}")]
    public async Task<object> GetProfile(string userId, string key) {
        var user = await userStore.GetUserById(userId, false);
        if (user is null)
            throw new MatrixException() {
                ErrorCode = "M_NOT_FOUND",
                Error = "User not found."
            };
        if (!user.Profile.TryGetValue(key, out var value))
            throw new MatrixException() {
                ErrorCode = "M_NOT_FOUND",
                Error = "Key not found."
            };
        return value;
    }

    [HttpPut("profile/{userId}/{key}")]
    public async Task<object> SetProfile(string userId, string key, [FromBody] JsonNode value) {
        var user = await userStore.GetUserById(userId, false);
        if (user is null)
            throw new MatrixException() {
                ErrorCode = "M_NOT_FOUND",
                Error = "User not found."
            };
        user.Profile[key] = value[key]?.AsObject();
        return value;
    }
}