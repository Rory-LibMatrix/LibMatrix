using LibMatrix.Services.WellKnownResolver.WellKnownResolvers;
using Microsoft.AspNetCore.Mvc;

namespace LibMatrix.FederationTest.Controllers.Spec;

[ApiController]
[Route(".well-known/")]
public class WellKnownController(ILogger<WellKnownController> logger) : ControllerBase {
    static WellKnownController() {
        Console.WriteLine("INFO | WellKnownController initialized.");
    }
    [HttpGet("matrix/server")]
    public ServerWellKnown GetMatrixServerWellKnown() {
        // {Request.Headers["X-Forwarded-Proto"].FirstOrDefault(Request.Scheme)}://
        return new() {
            Homeserver = $"{Request.Headers["X-Forwarded-Host"].FirstOrDefault(Request.Host.Host)}:{Request.Headers["X-Forwarded-Port"].FirstOrDefault("443")}",
        };
    }
}