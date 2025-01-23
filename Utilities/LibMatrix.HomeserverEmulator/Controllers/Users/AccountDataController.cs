using System.Text.Json.Nodes;
using LibMatrix.HomeserverEmulator.Services;
using Microsoft.AspNetCore.Mvc;

namespace LibMatrix.HomeserverEmulator.Controllers;

[ApiController]
[Route("/_matrix/client/{version}/")]
public class AccountDataController(ILogger<AccountDataController> logger, TokenService tokenService, UserStore userStore, RoomStore roomStore) : ControllerBase {
    [HttpGet("user/{mxid}/account_data/{type}")]
    public async Task<object> GetAccountData(string type) {
        var token = tokenService.GetAccessToken(HttpContext);
        var user = await userStore.GetUserByToken(token, false);
        if (user is null)
            throw new MatrixException() {
                ErrorCode = "M_UNAUTHORIZED",
                Error = "Invalid token."
            };

        var value = user.AccountData.FirstOrDefault(x => x.Type == type);
        if (value is null)
            throw new MatrixException() {
                ErrorCode = "M_NOT_FOUND",
                Error = "Key not found."
            };
        return value;
    }

    [HttpPut("user/{mxid}/account_data/{type}")]
    public async Task<object> SetAccountData(string type, [FromBody] JsonObject data) {
        var token = tokenService.GetAccessToken(HttpContext);
        var user = await userStore.GetUserByToken(token, false);
        if (user is null)
            throw new MatrixException() {
                ErrorCode = "M_UNAUTHORIZED",
                Error = "Invalid token."
            };

        user.AccountData.Add(new() {
            Type = type,
            RawContent = data
        });
        return data;
    }

    // specialised account data...
    [HttpGet("pushrules")]
    public async Task<object> GetPushRules() {
        var token = tokenService.GetAccessToken(HttpContext);
        if (token is null)
            throw new MatrixException() {
                ErrorCode = "M_UNAUTHORIZED",
                Error = "No token passed."
            };

        var user = await userStore.GetUserByToken(token, false);
        if (user is null)
            throw new MatrixException() {
                ErrorCode = "M_UNAUTHORIZED",
                Error = "Invalid token."
            };
        return new { };
    }
}