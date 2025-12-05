using LibMatrix.Abstractions;
using LibMatrix.Federation.Extensions;
using LibMatrix.FederationTest.Services;
using LibMatrix.Homeservers;
using LibMatrix.Responses.Federation;
using Microsoft.AspNetCore.Mvc;

namespace LibMatrix.FederationTest.Controllers.Spec;

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
                ValidUntil = DateTime.Now + TimeSpan.FromMinutes(5),
                ServerName = config.ServerName,
                OldVerifyKeys = [],
                VerifyKeysById = new() {
                    {
                        keys.CurrentSigningKey.KeyId, new ServerKeysResponse.CurrentVerifyKey() {
                            Key = keys.CurrentSigningKey.PublicKey //.ToUnpaddedBase64(),
                        }
                    }
                }
            }.Sign(keys.CurrentSigningKey);
        }

        _serverKeyCacheLock.Release();

        return _cachedServerKeysResponse;
    }
}