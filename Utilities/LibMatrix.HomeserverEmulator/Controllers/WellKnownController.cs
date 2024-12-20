using System.Text.Json.Nodes;
using LibMatrix.Services;
using Microsoft.AspNetCore.Mvc;

namespace LibMatrix.HomeserverEmulator.Controllers;

[ApiController]
[Route("/.well-known/matrix/")]
public class WellKnownController(ILogger<WellKnownController> logger) : ControllerBase {
    [HttpGet("client")]
    public JsonObject GetClientWellKnown() {
        var obj = new JsonObject() {
            ["m.homeserver"] = new JsonObject() {
                ["base_url"] = $"{Request.Scheme}://{Request.Host}"
            }
        };

        logger.LogInformation("Serving client well-known: {}", obj);

        return obj;
    }
    [HttpGet("server")]
    public JsonObject GetServerWellKnown() {
        var obj = new JsonObject() {
            ["m.server"] = $"{Request.Scheme}://{Request.Host}"
        };

        logger.LogInformation("Serving server well-known: {}", obj);

        return obj;
    }
}