using LibMatrix.Homeservers;
using LibMatrix.Responses.Federation;
using Microsoft.AspNetCore.Mvc;

namespace LibMatrix.FederationTest.Controllers.Spec;

[ApiController]
[Route("_matrix/federation/v1/")]
public class FederationVersionController : ControllerBase {
    [HttpGet("version")]
    public ServerVersionResponse GetVersion() {
        return new ServerVersionResponse {
            Server = new() {
                Name = "LibMatrix.Federation",
                Version = "0.0.0",
            }
        };
    }
}