using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ArcaneLibs.Extensions;
using LibMatrix.Abstractions;
using LibMatrix.Homeservers;

namespace LibMatrix.Responses.Federation;

[JsonConverter(typeof(SignedObjectConverterFactory))]
public class SignedObject<T> {
    [JsonPropertyName("signatures")]
    public Dictionary<string, Dictionary<string, string>> Signatures { get; set; } = new();

    [JsonIgnore]
    public Dictionary<string, Dictionary<VersionedKeyId, string>> SignaturesById {
        get => Signatures.ToDictionary(server => server.Key, server => server.Value.ToDictionary(key => (VersionedKeyId)key.Key, key => key.Value));
        set => Signatures = value.ToDictionary(server => server.Key, server => server.Value.ToDictionary(key => (string)key.Key, key => key.Value));
    }

    [JsonExtensionData]
    public required JsonObject Content { get; set; }

    [JsonIgnore]
    public T TypedContent {
        get => Content.Deserialize<T>() ?? throw new JsonException("Failed to deserialize TypedContent from Content.");
        set => Content = JsonSerializer.Deserialize<JsonObject>(JsonSerializer.Serialize(value)) ?? new JsonObject();
    }
}

public class SignedObjectConverter<T> : JsonConverter<SignedObject<T>> {
    public override SignedObject<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        var jsonObject = JsonSerializer.Deserialize<JsonObject>(ref reader, options);
        if (jsonObject == null) {
            throw new JsonException("Failed to deserialize SignedObject, JSON object is null.");
        }

        var signatures = jsonObject["signatures"] ?? throw new JsonException("Failed to find 'signatures' property in JSON object.");
        jsonObject.Remove("signatures");

        var signedObject = new SignedObject<T> {
            Content = jsonObject,
            Signatures = signatures.Deserialize<Dictionary<string, Dictionary<string, string>>>()
                         ?? throw new JsonException("Failed to deserialize 'signatures' property into Dictionary<string, Dictionary<string, string>>.")
        };

        return signedObject;
    }

    public override void Write(Utf8JsonWriter writer, SignedObject<T> value, JsonSerializerOptions options) {
        var targetObj = value.Content.DeepClone();
        targetObj["signatures"] = value.Signatures.ToJsonNode();
        JsonSerializer.Serialize(writer, targetObj, options);
    }
}

internal class SignedObjectConverterFactory : JsonConverterFactory {
    public override bool CanConvert(Type typeToConvert) {
        if (!typeToConvert.IsGenericType) return false;
        return typeToConvert.GetGenericTypeDefinition() == typeof(SignedObject<>);
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) {
        var wrappedType = typeToConvert.GetGenericArguments()[0];
        var converter = (JsonConverter)Activator.CreateInstance(typeof(SignedObjectConverter<>).MakeGenericType(wrappedType))!;
        return converter;
    }
}