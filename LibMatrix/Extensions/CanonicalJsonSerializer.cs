using System.Collections.Frozen;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using ArcaneLibs.Extensions;

namespace LibMatrix.Extensions;

public static class CanonicalJsonSerializer {
    // TODO: Alphabetise dictionaries
    private static JsonSerializerOptions _options => new() {
        WriteIndented = false,
        Encoder = UnicodeJsonEncoder.Singleton,
    };

    private static readonly FrozenSet<PropertyInfo> JsonSerializerOptionsProperties = typeof(JsonSerializerOptions)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(x => x.SetMethod != null && x.GetMethod != null)
        .ToFrozenSet();

    private static JsonSerializerOptions MergeOptions(JsonSerializerOptions? inputOptions) {
        var newOptions = _options;
        if (inputOptions == null)
            return newOptions;
        
        foreach (var property in JsonSerializerOptionsProperties) {
            if(property.Name == nameof(JsonSerializerOptions.Encoder))
                continue;
            if (property.Name == nameof(JsonSerializerOptions.WriteIndented))
                continue;
                
            var value = property.GetValue(inputOptions);
            // if (value == null)
                // continue;
            property.SetValue(newOptions, value);
        }

        return newOptions;
    }

#region STJ API

    public static String Serialize<TValue>(TValue value, JsonSerializerOptions? options = null) {
        var newOptions = MergeOptions(options);

        return System.Text.Json.JsonSerializer.SerializeToNode(value, options) // We want to allow passing custom converters for eg. double/float -> string here...
            .SortProperties()!
            .CanonicalizeNumbers()!
            .ToJsonString(newOptions);
        
            
        // System.Text.Json.JsonSerializer.SerializeToNode(System.Text.Json.JsonSerializer.Deserialize<dynamic>("{\n    \"a\": -0,\n    \"b\": 1e10\n}")).ToJsonString();
        
    }

    public static String Serialize(object value, Type inputType, JsonSerializerOptions? options = null) => JsonSerializer.Serialize(value, inputType, _options);
    // public static String Serialize<TValue>(TValue value, JsonTypeInfo<TValue> jsonTypeInfo) => JsonSerializer.Serialize(value, jsonTypeInfo, _options);
    // public static String Serialize(Object value, JsonTypeInfo jsonTypeInfo) 

#endregion

    private static partial class JsonExtensions {
        public static Action<JsonTypeInfo> AlphabetizeProperties(Type type) {
            return typeInfo => {
                if (typeInfo.Kind != JsonTypeInfoKind.Object || !type.IsAssignableFrom(typeInfo.Type))
                    return;
                AlphabetizeProperties()(typeInfo);
            };
        }

        public static Action<JsonTypeInfo> AlphabetizeProperties() {
            return static typeInfo => {
                if (typeInfo.Kind == JsonTypeInfoKind.Dictionary) { }

                if (typeInfo.Kind != JsonTypeInfoKind.Object)
                    return;
                var properties = typeInfo.Properties.OrderBy(p => p.Name, StringComparer.Ordinal).ToList();
                typeInfo.Properties.Clear();
                for (int i = 0; i < properties.Count; i++) {
                    properties[i].Order = i;
                    typeInfo.Properties.Add(properties[i]);
                }
            };
        }
    }
}