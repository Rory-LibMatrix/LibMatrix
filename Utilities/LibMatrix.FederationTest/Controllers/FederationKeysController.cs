using LibMatrix.Federation.Extensions;
using LibMatrix.Federation.Utilities;
using LibMatrix.FederationTest.Services;
using LibMatrix.Homeservers;
using Microsoft.AspNetCore.Mvc;

namespace LibMatrix.FederationTest.Controllers;

[ApiController]
[Route("_matrix/key/v2/")]
public class FederationKeysController(FederationTestConfiguration config, FederationKeyStore keyStore) {
    static FederationKeysController() {
        Console.WriteLine("INFO | FederationKeysController initialized.");
    }

    private static SignedObject<ServerKeysResponse>? _cachedServerKeysResponse;
    private static SemaphoreSlim _serverKeyCacheLock = new SemaphoreSlim(1, 1);

    [HttpGet("server")]
    public async Task<SignedObject<ServerKeysResponse>> GetServerKeys() {
        await _serverKeyCacheLock.WaitAsync();
        if (_cachedServerKeysResponse == null || _cachedServerKeysResponse.TypedContent.ValidUntil < DateTime.Now + TimeSpan.FromSeconds(30)) {
            var keys = keyStore.GetCurrentSigningKey();
            _cachedServerKeysResponse = new ServerKeysResponse() {
                ValidUntil = DateTime.Now + TimeSpan.FromMinutes(1),
                ServerName = config.ServerName,
                OldVerifyKeys = [],
                VerifyKeysById = new() {
                    {
                        new() { Algorithm = "ed25519", KeyId = "0" }, new ServerKeysResponse.CurrentVerifyKey() {
                            Key = keys.publicKey.ToUnpaddedBase64(),
                        }
                    }
                }
            }.Sign(config.ServerName, new ServerKeysResponse.VersionedKeyId() { Algorithm = "ed25519", KeyId = "0" }, keys.privateKey);
        }
        _serverKeyCacheLock.Release();

        return _cachedServerKeysResponse;
    }
}