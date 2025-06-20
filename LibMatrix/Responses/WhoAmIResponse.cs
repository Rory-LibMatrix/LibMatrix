using System.Text.Json.Serialization;

namespace LibMatrix.Responses;

public class WhoAmIResponse {
    [JsonPropertyName("user_id")]
    public required string UserId { get; set; }

    [JsonPropertyName("device_id")]
    public string? DeviceId { get; set; }

    [JsonPropertyName("is_guest")]
    public bool? IsGuest { get; set; }
}