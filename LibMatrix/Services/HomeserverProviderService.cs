using System.Net.Http.Json;
using ArcaneLibs.Collections;
using LibMatrix.Homeservers;
using LibMatrix.Responses;
using LibMatrix.Responses.Federation;
using Microsoft.Extensions.Logging;

namespace LibMatrix.Services;

public class HomeserverProviderService(ILogger<HomeserverProviderService> logger, HomeserverResolverService hsResolver) {
    private static readonly SemaphoreCache<AuthenticatedHomeserverGeneric> AuthenticatedHomeserverCache = new();
    private static readonly SemaphoreCache<RemoteHomeserver> RemoteHomeserverCache = new();
    private static readonly SemaphoreCache<FederationClient> FederationClientCache = new();

    public async Task<AuthenticatedHomeserverGeneric> GetAuthenticatedWithToken(string homeserver, string accessToken, string? proxy = null, string? impersonatedMxid = null,
        bool useGeneric = false, bool enableClient = true, bool enableServer = true) {
        if (!enableClient && !enableServer)
            throw new ArgumentException("At least one of enableClient or enableServer must be true");

        return await AuthenticatedHomeserverCache.GetOrAdd($"{homeserver}{accessToken}{proxy}{impersonatedMxid}", async () => {
            var wellKnownUris = await hsResolver.ResolveHomeserverFromWellKnown(homeserver, enableClient, enableServer);
            var rhs = new RemoteHomeserver(homeserver, wellKnownUris, proxy);

            AuthenticatedHomeserverGeneric? hs = null;
            if (!useGeneric) {
                var clientVersionsTask = rhs.GetClientVersionsAsync();
                var serverVersionTask = rhs.FederationClient?.GetServerVersionAsync() ?? Task.FromResult<ServerVersionResponse?>(null)!;
                ClientVersionsResponse clientVersions = new();
                try {
                    clientVersions = await clientVersionsTask;
                }
                catch (Exception e) {
                    logger.LogError(e, "Failed to get client versions for {homeserver}", homeserver);
                }

                ServerVersionResponse? serverVersion;
                try {
                    serverVersion = await serverVersionTask;
                }
                catch (Exception e) {
                    logger.LogWarning(e, "Failed to get server version for {homeserver}", homeserver);
                    throw;
                }

                try {
                    if (clientVersions.UnstableFeatures.TryGetValue("gay.rory.mxapiextensions.v0", out var a) && a)
                        hs = new AuthenticatedHomeserverMxApiExtended(homeserver, wellKnownUris, proxy, accessToken);
                    else {
                        if (serverVersion is { Server.Name: "Synapse" })
                            hs = new AuthenticatedHomeserverSynapse(homeserver, wellKnownUris, proxy, accessToken);
                        else if (serverVersion is { Server.Name: "LibMatrix.HomeserverEmulator"})
                            hs = new AuthenticatedHomeserverHSE(homeserver, wellKnownUris, proxy, accessToken);
                    }
                }
                catch (Exception e) {
                    logger.LogError(e, "Failed to create authenticated homeserver for {homeserver}", homeserver);
                    throw;
                }
            }

            hs ??= new AuthenticatedHomeserverGeneric(homeserver, wellKnownUris, proxy, accessToken);

            await hs.Initialise();

            if (impersonatedMxid is not null)
                await hs.SetImpersonate(impersonatedMxid);

            return hs;
        });
    }

    public async Task<RemoteHomeserver> GetRemoteHomeserver(string homeserver, string? proxy = null, bool useCache = true, bool enableServer = false) =>
        useCache
            ? await RemoteHomeserverCache.GetOrAdd($"{homeserver}{proxy}",
                async () => { return new RemoteHomeserver(homeserver, await hsResolver.ResolveHomeserverFromWellKnown(homeserver, enableServer: enableServer), proxy); })
            : new RemoteHomeserver(homeserver, await hsResolver.ResolveHomeserverFromWellKnown(homeserver, enableServer: enableServer), proxy);

    public async Task<FederationClient> GetFederationClient(string homeserver, string keyId, string? proxy = null, bool useCache = true) =>
        useCache
            ? await FederationClientCache.GetOrAdd($"{homeserver}{keyId}{proxy}",
                async () => { return new FederationClient((await hsResolver.ResolveHomeserverFromWellKnown(homeserver, enableClient: false)).Server!, proxy); })
            : new FederationClient((await hsResolver.ResolveHomeserverFromWellKnown(homeserver, enableClient: false)).Server!, proxy);

    public async Task<LoginResponse> Login(string homeserver, string user, string password, string? proxy = null) {
        var hs = await GetRemoteHomeserver(homeserver, proxy);
        var payload = new LoginRequest {
            Identifier = new LoginRequest.LoginIdentifier { User = user },
            Password = password
        };
        var resp = await hs.ClientHttpClient.PostAsJsonAsync("/_matrix/client/v3/login", payload);
        var data = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        return data!;
    }
}