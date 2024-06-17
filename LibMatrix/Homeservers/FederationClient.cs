using System.Text.Json.Serialization;
using LibMatrix.Extensions;
using LibMatrix.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace LibMatrix.Homeservers;

public class FederationClient {
    public FederationClient(string federationEndpoint, string? proxy = null) {
        HttpClient = new MatrixHttpClient {
            BaseAddress = new Uri(proxy?.TrimEnd('/') ?? federationEndpoint.TrimEnd('/')),
            // Timeout = TimeSpan.FromSeconds(120) // TODO: Re-implement this
        };
        if (proxy is not null) HttpClient.DefaultRequestHeaders.Add("MXAE_UPSTREAM", federationEndpoint);
    }

    public MatrixHttpClient HttpClient { get; set; } = null!;
    public HomeserverResolverService.WellKnownUris WellKnownUris { get; set; } = null!;

    public async Task<ServerVersionResponse> GetServerVersionAsync() => await HttpClient.GetFromJsonAsync<ServerVersionResponse>("/_matrix/federation/v1/version");
}

public class ServerVersionResponse {
    [JsonPropertyName("server")]
    public required ServerInfo Server { get; set; }

    public class ServerInfo {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("version")]
        public string Version { get; set; }
    }
}