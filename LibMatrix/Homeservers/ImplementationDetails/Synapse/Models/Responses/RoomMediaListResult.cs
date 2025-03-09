using System.Text.Json.Serialization;

namespace LibMatrix.Homeservers.ImplementationDetails.Synapse.Models.Responses;

public class SynapseAdminRoomMediaListResult {
    [JsonPropertyName("local")]
    public List<string> Local { get; set; } = new();

    [JsonPropertyName("remote")]
    public List<string> Remote { get; set; } = new();
}