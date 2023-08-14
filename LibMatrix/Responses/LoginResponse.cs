using System.Text.Json.Serialization;

namespace LibMatrix.Responses;

public class LoginResponse {
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; }

    [JsonPropertyName("device_id")]
    public string DeviceId { get; set; }

    [JsonPropertyName("home_server")]
    public string Homeserver { get; set; }

    [JsonPropertyName("user_id")]
    public string UserId { get; set; }
}
