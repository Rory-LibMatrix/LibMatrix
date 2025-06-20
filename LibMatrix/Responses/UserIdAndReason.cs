using System.Text.Json.Serialization;

namespace LibMatrix.Responses;

internal class UserIdAndReason(string userId = null!, string reason = null!) {
    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = userId;

    [JsonPropertyName("reason")]
    public string? Reason { get; set; } = reason;
}