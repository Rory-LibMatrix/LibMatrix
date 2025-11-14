using System.Text.Json.Serialization;

namespace LibMatrix.Responses;

public class MessagesResponse {
    [JsonPropertyName("start")]
    public string Start { get; set; }

    [JsonPropertyName("end")]
    public string? End { get; set; }

    [JsonPropertyName("chunk")]
    public List<MatrixEventResponse> Chunk { get; set; } = new();

    [JsonPropertyName("state")]
    public List<MatrixEventResponse> State { get; set; } = new();
}