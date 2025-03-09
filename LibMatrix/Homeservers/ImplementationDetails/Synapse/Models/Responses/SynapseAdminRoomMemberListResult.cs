using System.Text.Json.Serialization;

namespace LibMatrix.Homeservers.ImplementationDetails.Synapse.Models.Responses;

public class SynapseAdminRoomMemberListResult {
    [JsonPropertyName("members")]
    public List<string> Members { get; set; }
    
    [JsonPropertyName("total")]
    public int Total { get; set; }
}