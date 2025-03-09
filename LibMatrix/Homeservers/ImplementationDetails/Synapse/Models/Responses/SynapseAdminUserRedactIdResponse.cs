using System.Text.Json.Serialization;

namespace LibMatrix.Homeservers.ImplementationDetails.Synapse.Models.Responses;

public class SynapseAdminUserRedactIdResponse {
    [JsonPropertyName("redact_id")]
    public string RedactionId { get; set; }
}

public class SynapseAdminRedactStatusResponse {
    /// <summary>
    /// One of "scheduled", "active", "completed", "failed"
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; }
    
    /// <summary>
    /// Key: Event ID, Value: Error message
    /// </summary>
    [JsonPropertyName("failed_redactions")]
    public Dictionary<string, string> FailedRedactions { get; set; }
}