using System.Text.Json.Serialization;

namespace LibMatrix.Homeservers.ImplementationDetails.Synapse.Models.Responses;

public class SynapseAdminBackgroundUpdateStatusResponse {
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("current_updates")]
    public Dictionary<string, BackgroundUpdateInfo> CurrentUpdates { get; set; }

    public class BackgroundUpdateInfo {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("total_item_count")]
        public int TotalItemCount { get; set; }

        [JsonPropertyName("total_duration_ms")]
        public double TotalDurationMs { get; set; }

        [JsonPropertyName("average_items_per_ms")]
        public double AverageItemsPerMs { get; set; }

        [JsonIgnore]
        public TimeSpan TotalDuration => TimeSpan.FromMilliseconds(TotalDurationMs);
    }
}