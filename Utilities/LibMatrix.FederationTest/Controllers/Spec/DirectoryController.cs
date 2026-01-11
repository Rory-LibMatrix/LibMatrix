using System.Net.Http.Headers;
using LibMatrix.Federation;
using LibMatrix.FederationTest.Services;
using LibMatrix.Homeservers;
using Microsoft.AspNetCore.Mvc;

namespace LibMatrix.FederationTest.Controllers.Spec;

[ApiController]
[Route("_matrix/federation/")]
public class DirectoryController(ServerAuthService serverAuth) : ControllerBase {
    [HttpGet("v1/publicRooms")]
    [HttpPost("v1/publicRooms")]
    public async Task<IActionResult> GetPublicRooms() {
        if (Request.Headers.ContainsKey("Authorization")) {
            Console.WriteLine("INFO | Authorization header found.");
            await serverAuth.AssertValidAuthentication();
        }
        else Console.WriteLine("INFO | Room directory request without auth");

        var rooms = new List<PublicRoomDirectoryResult.PublicRoomListItem> {
            new() {
                GuestCanJoin = false,
                RoomId = "!tuiLEoMqNOQezxILzt:rory.gay",
                NumJoinedMembers = Random.Shared.Next(),
                WorldReadable = false,
                CanonicalAlias = "#libmatrix:rory.gay",
                Name = "Rory&::LibMatrix",
                Topic = $"A .NET {Environment.Version.Major} library for interacting with Matrix"
            }
        };
        return Ok(new PublicRoomDirectoryResult() {
            Chunk = rooms,
            TotalRoomCountEstimate = rooms.Count
        });
    }

    [HttpGet("v1/query/profile")]
    public async Task<IActionResult> GetProfile([FromQuery(Name = "user_id")] string userId) {
        if (Request.Headers.ContainsKey("Authorization")) {
            Console.WriteLine("INFO | Authorization header found.");
            await serverAuth.AssertValidAuthentication();
        }
        else Console.WriteLine("INFO | Profile request without auth");

        return Ok(new {
            avatar_url = "mxc://rory.gay/ocRVanZoUTCcifcVNwXgbtTg",
            displayname = "Rory&::LibMatrix.FederationTest"
        });
    }
}