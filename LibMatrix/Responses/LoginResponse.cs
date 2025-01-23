using System.Text.Json.Serialization;

namespace LibMatrix.Responses;

public class LoginResponse {
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = null!;

    [JsonPropertyName("device_id")]
    public string DeviceId { get; set; } = null!;

    private string? _homeserver;

    [JsonPropertyName("home_server")]
    public string Homeserver {
        get => _homeserver ?? UserId.Split(':', 2).Last();
        protected init => _homeserver = value;
    }

    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = null!;

    // public async Task<AuthenticatedHomeserverGeneric> GetAuthenticatedHomeserver(string? proxy = null) {
        // var urls = await new HomeserverResolverService().ResolveHomeserverFromWellKnown(Homeserver);
        // await AuthenticatedHomeserverGeneric.Create<AuthenticatedHomeserverGeneric>(Homeserver, AccessToken, proxy);
    // }
}

public class LoginRequest {
    [JsonPropertyName("type")]
    public string Type { get; set; } = "m.login.password";

    [JsonPropertyName("identifier")]
    public LoginIdentifier Identifier { get; set; } = new();

    [JsonPropertyName("password")]
    public string Password { get; set; } = "";

    [JsonPropertyName("initial_device_display_name")]
    public string InitialDeviceDisplayName { get; set; } = "Rory&::LibMatrix";

    public class LoginIdentifier {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "m.id.user";

        [JsonPropertyName("user")]
        public string User { get; set; } = "";
    }
}