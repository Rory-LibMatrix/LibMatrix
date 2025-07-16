using LibMatrix.Extensions;
using LibMatrix.Responses.Federation;
using LibMatrix.Services;

namespace LibMatrix.Homeservers;

public class FederationClient {
    public FederationClient(string federationEndpoint, string? proxy = null) {
        HttpClient = new MatrixHttpClient {
            BaseAddress = new Uri(proxy?.TrimEnd('/') ?? federationEndpoint.TrimEnd('/')),
            // Timeout = TimeSpan.FromSeconds(120) // TODO: Re-implement this
        };
        if (proxy is not null) HttpClient.DefaultRequestHeaders.Add("MXAE_UPSTREAM", federationEndpoint);
    }

    public MatrixHttpClient HttpClient { get; set; }
    public HomeserverResolverService.WellKnownUris WellKnownUris { get; set; }

    public async Task<ServerVersionResponse> GetServerVersionAsync() => await HttpClient.GetFromJsonAsync<ServerVersionResponse>("/_matrix/federation/v1/version");
    public async Task<SignedObject<ServerKeysResponse>> GetServerKeysAsync() => await HttpClient.GetFromJsonAsync<SignedObject<ServerKeysResponse>>("/_matrix/key/v2/server");
}


