using System.Text.Json.Serialization;

namespace LibMatrix.Responses;

public class DeviceKeysUploadRequest {
    [JsonPropertyName("device_keys")]
    public DeviceKeysSchema DeviceKeys { get; set; }


    [JsonPropertyName("one_time_keys")]
    public Dictionary<string, OneTimeKey> OneTimeKeys { get; set; }

    public class DeviceKeysSchema {
        [JsonPropertyName("algorithms")]
        public List<string> Algorithms { get; set; }
    }
    public class OneTimeKey {
        [JsonPropertyName("key")]
        public string Key { get; set; }
        
        [JsonPropertyName("signatures")]
        public Dictionary<string, Dictionary<string, string>> Signatures { get; set; }
    }
}