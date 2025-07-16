using System.Text.Json.Nodes;
using LibMatrix.Abstractions;
using LibMatrix.Extensions;
using LibMatrix.Federation;
using LibMatrix.Federation.Extensions;
using LibMatrix.FederationTest.Services;
using LibMatrix.Homeservers;
using Microsoft.AspNetCore.Mvc;

namespace LibMatrix.FederationTest.Controllers;

[ApiController]
public class TestController(FederationTestConfiguration config, FederationKeyStore keyStore) : ControllerBase {
    static TestController() {
        Console.WriteLine("INFO | TestController initialized.");
    }

    [HttpGet("/test")]
    public async Task<JsonObject> GetTest() {
        var hc = new MatrixHttpClient() {
            BaseAddress = new Uri("https://matrix.rory.gay")
        };

        var keyId = new VersionedKeyId() {
            Algorithm = "ed25519",
            KeyId = "0"
        };

        var signatureData = new XMatrixAuthorizationScheme.XMatrixRequestSignature() {
                Method = "GET",
                Uri = "/_matrix/federation/v1/user/devices/@emma:rory.gay",
                OriginServerName = config.ServerName,
                DestinationServerName = "rory.gay"
            }
            .Sign(config.ServerName, keyId, keyStore.GetCurrentSigningKey().privateKey);

        var signature = signatureData.Signatures[config.ServerName][keyId];
        var headerValue = new XMatrixAuthorizationScheme.XMatrixAuthorizationHeader() {
            Origin = config.ServerName,
            Destination = "rory.gay",
            Key = keyId,
            Signature = signature
        }.ToHeaderValue();

        var req = new HttpRequestMessage(HttpMethod.Get, "/_matrix/federation/v1/user/devices/@emma:rory.gay");
        req.Headers.Add("Authorization", headerValue);

        var response = await hc.SendAsync(req);
        var content = await response.Content.ReadFromJsonAsync<JsonObject>();
        return content!;
    }
}