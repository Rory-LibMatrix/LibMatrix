using LibMatrix.Homeservers.ImplementationDetails.Synapse;
using LibMatrix.Responses;
using LibMatrix.Services;

namespace LibMatrix.Homeservers;

public class AuthenticatedHomeserverHSE : AuthenticatedHomeserverGeneric {
    public AuthenticatedHomeserverHSE(string serverName, HomeserverResolverService.WellKnownUris wellKnownUris, string? proxy, string accessToken) : base(serverName,
        wellKnownUris, proxy, accessToken) { }

    public Task<Dictionary<string, LoginResponse>> GetExternalProfilesAsync() =>
        ClientHttpClient.GetFromJsonAsync<Dictionary<string, LoginResponse>>("/_hse/client/v1/external_profiles");

    public Task SetExternalProfile(string sessionName, LoginResponse session) =>
        ClientHttpClient.PutAsJsonAsync($"/_hse/client/v1/external_profiles/{sessionName}", session);
}