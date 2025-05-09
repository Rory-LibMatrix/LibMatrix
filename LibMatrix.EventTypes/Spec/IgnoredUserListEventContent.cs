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

        public T? GetAdditionalData<T>(string key) where T : class {
            if (AdditionalData == null || !AdditionalData.TryGetValue(key, out var value))
                return null;
            
            if (value is T tValue)
                return tValue;
            if (value is JsonElement jsonElement)
                return jsonElement.Deserialize<T>();

            throw new InvalidCastException($"Value for key '{key}' ({value.GetType()}) cannot be cast to type '{typeof(T)}'. Cannot continue.");
        }
    }
}