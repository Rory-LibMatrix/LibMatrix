using System.Text.Json.Serialization;

namespace LibMatrix.Responses.Federation;

public class ServerVersionResponse {
    [JsonPropertyName("server")]
    public required ServerInfo Server { get; set; }

    public class ServerInfo {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("version")]
        public string Version { get; set; }
    }
}