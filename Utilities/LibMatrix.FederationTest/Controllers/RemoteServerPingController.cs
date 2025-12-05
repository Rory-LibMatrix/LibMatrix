using LibMatrix.Federation;
using LibMatrix.Federation.Extensions;
using LibMatrix.FederationTest.Services;
using LibMatrix.FederationTest.Utilities;
using LibMatrix.Services;
using Microsoft.AspNetCore.Mvc;

namespace LibMatrix.FederationTest.Controllers;

[ApiController]
public class RemoteServerPingController(FederationTestConfiguration config, FederationKeyStore keyStore, HomeserverResolverService hsResolver) : ControllerBase {
    [HttpGet]
    [Route("/ping/{serverName}")]
    public async Task<object> PingRemoteServer(string serverName) {
        Dictionary<string, object> responseMessage = [];
        var hsResolveResult = await hsResolver.ResolveHomeserverFromWellKnown(serverName, enableClient: false);
        responseMessage["resolveResult"] = hsResolveResult;

        if (!string.IsNullOrWhiteSpace(hsResolveResult.Server)) {
            try {
                var ownKey = keyStore.GetCurrentSigningKey();
                var hs = new AuthenticatedFederationClient(hsResolveResult.Server, new() {
                    PrivateKey = ownKey.CurrentSigningKey,
                    OriginServerName = config.ServerName
                });
                var keys = await hs.GetServerKeysAsync();
                responseMessage["version"] = await hs.GetServerVersionAsync();
                responseMessage["keys"] = keys;

                responseMessage["keysAreValid"] = keys.SignaturesById[serverName].ToDictionary(
                    sig => (string)sig.Key,
                    sig => keys.ValidateSignature(serverName, sig.Key, Ed25519Utils.LoadPublicKeyFromEncoded(keys.TypedContent.VerifyKeysById[sig.Key].Key))
                );
            }
            catch (Exception ex) {
                responseMessage["error"] = new {
                    error = "Failed to connect to remote server",
                    message = ex.Message,
                    st = ex.StackTrace,
                };
                return responseMessage;
            }
        }

        return responseMessage;
    }

    [HttpPost]
    [Route("/ping/")]
    public async IAsyncEnumerable<KeyValuePair<string, object>> PingRemoteServers([FromBody] List<string>? serverNames) {
        Dictionary<string, object> responseMessage = [];

        if (serverNames == null || !serverNames.Any()) {
            responseMessage["error"] = "No server names provided";
            yield return responseMessage.First();
            yield break;
        }

        var results = serverNames!.Select(s => (s, PingRemoteServer(s))).ToList();
        foreach (var result in results) {
            var (serverName, pingResult) = result;
            try {
                responseMessage[serverName] = await pingResult;
                if (results.Where(x => !x.Item2.IsCompleted).Select(x => x.s).ToList() is { } servers and not { Count: 0 })
                    Console.WriteLine($"INFO | Waiting for servers: {string.Join(", ", servers)}");
            }
            catch (Exception ex) {
                responseMessage[serverName] = new {
                    error = "Failed to ping remote server",
                    message = ex.Message,
                    st = ex.StackTrace,
                };
            }

            yield return new KeyValuePair<string, object>(serverName, responseMessage[serverName]);
            // await Response.Body.FlushAsync();
        }
    }
}