using LibMatrix.HomeserverEmulator.Services;
using Microsoft.AspNetCore.Mvc;

namespace LibMatrix.HomeserverEmulator.Controllers;

[ApiController]
[Route("/_hsEmulator")]
public class HEDebugController(ILogger<HEDebugController> logger, UserStore userStore, RoomStore roomStore) : ControllerBase {
    [HttpGet("users")]
    public async Task<List<UserStore.User>> GetUsers() {
        return userStore._users;
    }
    
    [HttpGet("rooms")]
    public async Task<List<RoomStore.Room>> GetRooms() {
        return roomStore._rooms.ToList();
    }
}