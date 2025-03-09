using System.Text.Json.Serialization;

namespace LibMatrix.Homeservers.ImplementationDetails.Synapse.Models.Responses;

public class SynapseAdminUserMediaResult {
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("next_token")]
    public string? NextToken { get; set; }

    [JsonPropertyName("media")]
    public List<MediaInfo> Media { get; set; } = new();

    public class MediaInfo {
        [JsonPropertyName("created_ts")]
        public long CreatedTimestamp { get; set; }

        [JsonPropertyName("last_access_ts")]
        public long? LastAccessTimestamp { get; set; }

        [JsonPropertyName("media_id")]
        public string MediaId { get; set; }

        [JsonPropertyName("media_length")]
        public int MediaLength { get; set; }

        [JsonPropertyName("media_type")]
        public string MediaType { get; set; }

        [JsonPropertyName("quarantined_by")]
        public string? QuarantinedBy { get; set; }

        [JsonPropertyName("safe_from_quarantine")]
        public bool SafeFromQuarantine { get; set; }

        [JsonPropertyName("upload_name")]
        public string UploadName { get; set; }
    }
}