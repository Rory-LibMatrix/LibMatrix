namespace LibMatrix.Homeservers.ImplementationDetails.Synapse;

public class SynapseAdminUserCleanupExecutor(AuthenticatedHomeserverSynapse homeserver) {
    /*
       Remove mappings of SSO IDs
       Delete media uploaded by user (included avatar images)
       Delete sent and received messages
       Remove the user's creation (registration) timestamp
       Remove rate limit overrides
       Remove from monthly active users
       Remove user's consent information (consent version and timestamp)
     */
    public async Task CleanupUser(string mxid) {
        // change the user's password to a random one
        var newPassword = Guid.NewGuid().ToString();
        await homeserver.Admin.ResetPasswordAsync(mxid, newPassword, true);
        await homeserver.Admin.DeleteAllMessages(mxid);
        
    }
    private async Task RunUserTasks(string mxid) {
        var auth = await homeserver.Admin.LoginUserAsync(mxid, TimeSpan.FromDays(1));
        var userHs = new AuthenticatedHomeserverSynapse(homeserver.ServerName, homeserver.WellKnownUris, null, auth.AccessToken);
        await userHs.Initialise();
        
        
    }
}