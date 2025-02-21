using ArcaneLibs.Collections;
using LibMatrix.HomeserverEmulator.Services;
using LibMatrix.Responses;
using Microsoft.AspNetCore.Mvc;

namespace LibMatrix.HomeserverEmulator.Controllers;

[ApiController]
[Route("/_hse/client/v1/external_profiles")]
public class HEClientController(ILogger<HEClientController> logger, UserStore userStore, TokenService tokenService) : ControllerBase {
    [HttpGet]
    public async Task<ObservableDictionary<string, LoginResponse>> GetExternalProfiles() {
        var token = tokenService.GetAccessToken(HttpContext);
        var user = await userStore.GetUserByToken(token);

        return user.AuthorizedSessions;
    }

    [HttpPut("{name}")]
    public async Task PutExternalProfile(string name, [FromBody] LoginResponse sessionData) {
        var token = tokenService.GetAccessToken(HttpContext);
        var user = await userStore.GetUserByToken(token);

        user.AuthorizedSessions[name] = sessionData;
    }

    [HttpDelete("{name}")]
    public async Task DeleteExternalProfile(string name) {
        var token = tokenService.GetAccessToken(HttpContext);
        var user = await userStore.GetUserByToken(token);

        user.AuthorizedSessions.Remove(name);
    }
}