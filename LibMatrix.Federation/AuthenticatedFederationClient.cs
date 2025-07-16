using LibMatrix.Abstractions;
using LibMatrix.Federation.Extensions;
using LibMatrix.Homeservers;

namespace LibMatrix.Federation;

public class AuthenticatedFederationClient(string federationEndpoint, AuthenticatedFederationClient.AuthenticatedFederationConfiguration config, string? proxy = null) : FederationClient(federationEndpoint, proxy) {
    
    public class AuthenticatedFederationConfiguration {
        public required VersionedHomeserverPrivateKey PrivateKey { get; set; } 
        public required string OriginServerName { get; set; }
    }
    
    public async Task<UserDeviceListResponse> GetUserDevicesAsync(string userId) {
        var response = await HttpClient.SendAsync(new XMatrixAuthorizationScheme.XMatrixRequestSignature() {
            OriginServerName = config.OriginServerName,
            DestinationServerName = userId.Split(':', 2)[1],
            Method = "GET",
            Uri = $"/_matrix/federation/v1/user/devices/{userId}",
        }.ToSignedHttpRequestMessage(config.PrivateKey));
        return response;
    }
    
}