using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace LibMatrix.EventTypes.Spec;

[MatrixEvent(EventName = EventId)]
public class IgnoredUserListEventContent : EventContent {
    public const string EventId = "m.ignored_user_list";

    [JsonPropertyName("ignored_users")]
    public Dictionary<string, IgnoredUserContent> IgnoredUsers { get; set; } = new();

    // Dummy type to provide easy access to the by-spec empty content
    public class IgnoredUserContent {
        [JsonExtensionData]
        public Dictionary<string, object>? AdditionalData { get; set; } = [];

        public T GetAdditionalData<T>(string key) {
            if (AdditionalData == null || !AdditionalData.TryGetValue(key, out var value))
                throw new KeyNotFoundException($"Key '{key}' not found in AdditionalData.");
            if (value is T tValue)
                return tValue;
            throw new InvalidCastException($"Value for key '{key}' cannot be cast to type '{typeof(T)}'. Cannot continue.");
        }
    }
}