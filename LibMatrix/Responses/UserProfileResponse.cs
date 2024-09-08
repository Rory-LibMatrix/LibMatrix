using System.Text.Json;
using System.Text.Json.Serialization;

namespace LibMatrix.Responses;

public class UserProfileResponse {
    [JsonPropertyName("avatar_url")]
    public string? AvatarUrl { get; set; }

    [JsonPropertyName("displayname")]
    public string? DisplayName { get; set; }

    // MSC 4133 - Extending User Profile API with Key:Value pairs
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? CustomKeys { get; set; }

    public JsonElement? this[string key] {
        get => CustomKeys?[key];
        set {
            if (value is null)
                CustomKeys?.Remove(key);
            else
                (CustomKeys ??= [])[key] = value.Value;
        }
    }
}