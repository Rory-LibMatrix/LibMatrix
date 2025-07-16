using System.Text.Json.Serialization;
using LibMatrix.Homeservers;
using LibMatrix.Responses;
using LibMatrix.Responses.Federation;
using Microsoft.AspNetCore.Mvc;

namespace LibMatrix.HomeserverEmulator.Controllers;

[ApiController]
[Route("/_matrix/")]
public class VersionsController(ILogger<WellKnownController> logger) : ControllerBase {
    [HttpGet("client/versions")]
    public async Task<ClientVersionsResponse> GetClientVersions() {
        var clientVersions = new ClientVersionsResponse {
            Versions = new() {
                "r0.0.1",
                "r0.1.0",
                "r0.2.0",
                "r0.3.0",
                "r0.4.0",
                "r0.5.0",
                "r0.6.0",
                "r0.6.1",
                "v1.1",
                "v1.2",
                "v1.3",
                "v1.4",
                "v1.5",
                "v1.6",
                "v1.7",
                "v1.8",
                "v1.9",
                "v1.10",
                "v1.11",
                "v1.12",
                "v1.13"
            },
            UnstableFeatures = new()
        };
        return clientVersions;
    }

    [HttpGet("federation/v1/version")]
    public async Task<ServerVersionResponse> GetServerVersions() {
        var clientVersions = new ServerVersionResponse() {
            Server = new() {
                Name = "LibMatrix.HomeserverEmulator",
                Version = "0.0.0"
            }
        };
        return clientVersions;
    }

    [HttpGet("client/{version}/capabilities")]
    public async Task<CapabilitiesResponse> GetCapabilities() {
        var caps = new CapabilitiesResponse() {
            Capabilities = new() {
                ChangePassword = new() {
                    Enabled = false
                },
                RoomVersions = new() {
                    Default = "11",
                    Available = []
                }
            }
        };

        for (int i = 0; i < 15; i++) {
            caps.Capabilities.RoomVersions.Available.Add(i.ToString(), "stable");
        }

        return caps;
    }
}

public class CapabilitiesResponse {
    [JsonPropertyName("capabilities")]
    public CapabilitiesContent Capabilities { get; set; }

    public class CapabilitiesContent {
        [JsonPropertyName("m.room_versions")]
        public RoomVersionsContent RoomVersions { get; set; }

        [JsonPropertyName("m.change_password")]
        public ChangePasswordContent ChangePassword { get; set; }

        public class ChangePasswordContent {
            [JsonPropertyName("enabled")]
            public bool Enabled { get; set; }
        }

        public class RoomVersionsContent {
            [JsonPropertyName("default")]
            public string Default { get; set; }

            [JsonPropertyName("available")]
            public Dictionary<string, string> Available { get; set; }
        }
    }
}