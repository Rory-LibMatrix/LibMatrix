using LibMatrix.Filters;
using LibMatrix.HomeserverEmulator.Services;
using Microsoft.AspNetCore.Mvc;

namespace LibMatrix.HomeserverEmulator.Controllers;

[ApiController]
[Route("/_matrix/client/{version}/")]
public class FilterController(ILogger<FilterController> logger, TokenService tokenService, UserStore userStore, RoomStore roomStore) : ControllerBase {
    [HttpPost("user/{mxid}/filter")]
    public async Task<object> CreateFilter(string mxid, [FromBody] SyncFilter filter) {
        var token = tokenService.GetAccessToken(HttpContext);
        var user = await userStore.GetUserByToken(token, false);
        if (user is null)
            throw new MatrixException() {
                ErrorCode = "M_UNAUTHORIZED",
                Error = "Invalid token."
            };
        var filterId = Guid.NewGuid().ToString();
        user.Filters[filterId] = filter;
        return new {
            filter_id = filterId
        };
    }
    
    [HttpGet("user/{mxid}/filter/{filterId}")]
    public async Task<SyncFilter> GetFilter(string mxid, string filterId) {
        var token = tokenService.GetAccessToken(HttpContext);
        var user = await userStore.GetUserByToken(token, false);
        if (user is null)
            throw new MatrixException() {
                ErrorCode = "M_UNAUTHORIZED",
                Error = "Invalid token."
            };
        if (!user.Filters.ContainsKey(filterId))
            throw new MatrixException() {
                ErrorCode = "M_NOT_FOUND",
                Error = "Filter not found."
            };
        return user.Filters[filterId];
    }
}