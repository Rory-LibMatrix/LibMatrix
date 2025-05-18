using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace LibMatrix.Responses;

public class LoginResponse {
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; }

    [JsonPropertyName("device_id")]
    public string DeviceId { get; set; }

    [JsonPropertyName("home_server")]
    [field: AllowNull, MaybeNull]
    public string Homeserver {
        get => field ?? UserId.Split(':', 2).Last();
        set;
    }

    [JsonPropertyName("user_id")]
    public string UserId { get; set; }
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