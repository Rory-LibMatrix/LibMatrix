using System.Text.Json.Nodes;
using LibMatrix.Extensions;
using LibMatrix.Federation;
using LibMatrix.Federation.Extensions;
using LibMatrix.FederationTest.Services;
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

        var currentKey = keyStore.GetCurrentSigningKey().CurrentSigningKey;

        var signatureData = new XMatrixAuthorizationScheme.XMatrixRequestSignature() {
            OriginServerName = config.ServerName,
            Method = "GET",
            DestinationServerName = "rory.gay",
            Uri = "/_matrix/federation/v1/user/devices/@emma:rory.gay",
        };
        //     .Sign(currentKey);
        //
        // var signature = signatureData.Signatures[config.ServerName][currentKey.KeyId];
        // var headerValue = new XMatrixAuthorizationScheme.XMatrixAuthorizationHeader() {
        //     Origin = config.ServerName,
        //     Key = currentKey.KeyId,
        //     Destination = "rory.gay",
        //     Signature = signature
        // }.ToHeaderValue();

        // var req = new HttpRequestMessage(HttpMethod.Get, "/_matrix/federation/v1/user/devices/@emma:rory.gay");
        // req.Headers.Add("Authorization", headerValue);

        var req = signatureData.ToSignedHttpRequestMessage(currentKey);
        var response = await hc.SendAsync(req);
        var content = await response.Content.ReadFromJsonAsync<JsonObject>();
        return content!;
    }

    // [HttpGet("/testMakeJoin")]
    // public async Task<JsonObject> GetTestMakeJoin() { }
}