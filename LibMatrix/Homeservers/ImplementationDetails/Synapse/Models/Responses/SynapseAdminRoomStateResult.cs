using System.Text.Json.Serialization;

namespace LibMatrix.Homeservers.ImplementationDetails.Synapse.Models.Responses;

public class SynapseAdminRoomStateResult {
    [JsonPropertyName("state")]
    public required List<MatrixEventResponse> Events { get; set; }
}