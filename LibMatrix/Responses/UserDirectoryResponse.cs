using System.Text.Json.Serialization;

namespace LibMatrix.Responses;

public class UserDirectoryResponse {
    [JsonPropertyName("limited")]
    public bool Limited { get; set; }
    
    [JsonPropertyName("results")]
    public List<UserDirectoryResult> Results { get; set; }

    public class UserDirectoryResult {
        [JsonPropertyName("avatar_url")]
        public string? AvatarUrl { get; set; }
        
        [JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }
        
        [JsonPropertyName("user_id")]
        public string UserId { get; set; }
    }
}

public class UserDirectoryRequest {
    [JsonPropertyName("search_term")]
    public string SearchTerm { get; set; }
    
    [JsonPropertyName("limit")]
    public int? Limit { get; set; }
}