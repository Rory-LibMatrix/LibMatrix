using System.Net.Http.Headers;
using LibMatrix.Federation;
using LibMatrix.Federation.FederationTypes;
using LibMatrix.FederationTest.Services;
using Microsoft.AspNetCore.Mvc;

namespace LibMatrix.FederationTest.Controllers.Spec;

[ApiController]
[Route("_matrix/federation/")]
public class MembershipsController(ServerAuthService sas) : ControllerBase {
    [HttpGet("v1/make_join/{roomId}/{userId}")]
    [HttpPut("v1/send_join/{roomId}/{eventId}")]
    [HttpPut("v2/send_join/{roomId}/{eventId}")]
    [HttpGet("v1/make_knock/{roomId}/{userId}")]
    [HttpPut("v1/send_knock/{roomId}/{eventId}")]
    [HttpGet("v1/make_leave/{roomId}/{eventId}")]
    [HttpPut("v1/send_leave/{roomId}/{eventId}")]
    [HttpPut("v2/send_leave/{roomId}/{eventId}")]
    public async Task<IActionResult> JoinKnockMemberships() {
        await sas.AssertValidAuthentication();
        return NotFound(new MatrixException() {
            ErrorCode = MatrixException.ErrorCodes.M_NOT_FOUND,
            Error = "Rory&::LibMatrix.FederationTest does not support membership events."
        }.GetAsObject());
    }

    // [HttpPut("v1/invite/{roomId}/{eventId}")]
    [HttpPut("v2/invite/{roomId}/{eventId}")]
    public async Task<IActionResult> InviteHandler([FromBody] RoomInvite invite) {
        await sas.AssertValidAuthentication();

        Console.WriteLine($"Received invite event from {invite.Event.Sender} for room {invite.Event.RoomId} (version {invite.RoomVersion})\n" +
                          $"{invite.InviteRoomState.Count} invite room state events.");

        return NotFound(new MatrixException() {
            ErrorCode = MatrixException.ErrorCodes.M_NOT_FOUND,
            Error = "Rory&::LibMatrix.FederationTest does not support membership events."
        }.GetAsObject());
    }
}