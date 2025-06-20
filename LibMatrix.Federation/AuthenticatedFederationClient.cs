using LibMatrix.Homeservers;

namespace LibMatrix.Federation;

public class AuthenticatedFederationClient : FederationClient {
    
    public class AuthenticatedFederationConfiguration {
        
    }
    public AuthenticatedFederationClient(string federationEndpoint, AuthenticatedFederationConfiguration config, string? proxy = null) : base(federationEndpoint, proxy)
    {
        
    }
    
    // public async Task<UserDeviceListResponse> GetUserDevicesAsync(string userId) {
    //     var response = await GetAsync<UserDeviceListResponse>($"/_matrix/federation/v1/user/devices/{userId}", accessToken);
    //     return response;
    // }
    
}