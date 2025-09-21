using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace LibMatrix.EventTypes.Spec;

[MatrixEvent(EventName = EventId)]
public class Msc2545EmoteRoomsAccountDataEventContent : EventContent {
    public const string EventId = "im.ponies.emote_rooms";

    [JsonPropertyName("rooms")]
    public Dictionary<string, Dictionary<string, EnabledEmotePackEntry>> Rooms { get; set; } = new();

    // Dummy type to provide easy access to the by-spec empty content
    public class EnabledEmotePackEntry {
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