using LibMatrix.Homeservers;

namespace LibMatrix.Federation;

public class AuthenticatedFederationClient : FederationClient {
    public class AuthenticatedFederationConfiguration {
        
    }
    public AuthenticatedFederationClient(string federationEndpoint, AuthenticatedFederationConfiguration config, string? proxy = null) : base(federationEndpoint, proxy)
    {
        
    }
    
}