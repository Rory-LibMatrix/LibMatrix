using LibMatrix.Homeservers.ImplementationDetails.Synapse;
using LibMatrix.Services;

namespace LibMatrix.Homeservers;

public class AuthenticatedHomeserverSynapse : AuthenticatedHomeserverGeneric {
    public readonly SynapseAdminApiClient Admin;

    public AuthenticatedHomeserverSynapse(string serverName, HomeserverResolverService.WellKnownUris wellKnownUris, string? proxy, string accessToken) : base(serverName,
        wellKnownUris, proxy, accessToken) {
        Admin = new(this);
    }

}