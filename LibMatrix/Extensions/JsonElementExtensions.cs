using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace LibMatrix.Extensions;

public static class JsonElementExtensions {
    public static bool FindExtraJsonElementFields(this JsonElement obj, Type objectType, string objectPropertyName) {
        if (objectPropertyName == "content" && objectType == typeof(JsonObject))
            objectType = typeof(StateEventResponse);
        // if (t == typeof(JsonNode))
        //     return false;

        Console.WriteLine($"{objectType.Name} {objectPropertyName}");
        var unknownPropertyFound = false;
        var mappedPropsDict = objectType.GetProperties()
            .Where(x => x.GetCustomAttribute<JsonPropertyNameAttribute>() is not null)
            .ToDictionary(x => x.GetCustomAttribute<JsonPropertyNameAttribute>()!.Name, x => x);
        objectType.GetProperties().Where(x => !mappedPropsDict.ContainsKey(x.Name))
            .ToList().ForEach(x => mappedPropsDict.TryAdd(x.Name, x));

        foreach (var field in obj.EnumerateObject()) {
            if (mappedPropsDict.TryGetValue(field.Name, out var mappedProperty)) {
                //dictionary
                if (mappedProperty.PropertyType.IsGenericType &&
                    mappedProperty.PropertyType.GetGenericTypeDefinition() == typeof(Dictionary<,>)) {
                    unknownPropertyFound |= _checkDictionary(field, objectType, mappedProperty.PropertyType);
                    continue;
                }

                if (mappedProperty.PropertyType.IsGenericType &&
                    mappedProperty.PropertyType.GetGenericTypeDefinition() == typeof(List<>)) {
                    unknownPropertyFound |= _checkList(field, objectType, mappedProperty.PropertyType);
                    continue;
                }

                if (field.Name == "content" && (objectType == typeof(StateEventResponse) || objectType == typeof(StateEvent))) {
                    unknownPropertyFound |= field.FindExtraJsonPropertyFieldsByValueKind(
                        StateEvent.GetStateEventType(obj.GetProperty("type").GetString()!), // We expect type to always be present
                        mappedProperty.PropertyType);
                    continue;
                }

                unknownPropertyFound |=
                    field.FindExtraJsonPropertyFieldsByValueKind(objectType, mappedProperty.PropertyType);
                continue;
            }

            Console.WriteLine($"[!!] Unknown property {field.Name} in {objectType.Name}!");
            unknownPropertyFound = true;
        }

        return unknownPropertyFound;
    }

    private static bool FindExtraJsonPropertyFieldsByValueKind(this JsonProperty field, Type containerType,
        Type propertyType) {
        if (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(Nullable<>)) propertyType = propertyType.GetGenericArguments()[0];

        var switchResult = false;
        switch (field.Value.ValueKind) {
            case JsonValueKind.Array:
                switchResult = field.Value.EnumerateArray().Aggregate(switchResult,
                    (current, element) => current | element.FindExtraJsonElementFields(propertyType, field.Name));
                break;
            case JsonValueKind.Object:
                switchResult |= field.Value.FindExtraJsonElementFields(propertyType, field.Name);
                break;
            case JsonValueKind.True:
            case JsonValueKind.False:
                return _checkBool(field, containerType, propertyType);
            case JsonValueKind.String:
                return _checkString(field, containerType, propertyType);
            case JsonValueKind.Number:
                return _checkNumber(field, containerType, propertyType);
            case JsonValueKind.Undefined:
            case JsonValueKind.Null:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        return switchResult;
    }

    private static bool _checkBool(this JsonProperty field, Type containerType, Type propertyType) {
        if (propertyType == typeof(bool)) return true;
        Console.WriteLine(
            $"[!!] Encountered bool for {field.Name} in {containerType.Name}, the class defines {propertyType.Name}!");
        return false;
    }

    private static bool _checkString(this JsonProperty field, Type containerType, Type propertyType) {
        if (propertyType == typeof(string)) return true;
        // ReSharper disable once BuiltInTypeReferenceStyle
        if (propertyType == typeof(String)) return true;
        Console.WriteLine(
            $"[!!] Encountered string for {field.Name} in {containerType.Name}, the class defines {propertyType.Name}!");
        return false;
    }

    private static bool _checkNumber(this JsonProperty field, Type containerType, Type propertyType) {
        if (propertyType == typeof(int) ||
            propertyType == typeof(double) ||
            propertyType == typeof(float) ||
            propertyType == typeof(decimal) ||
            propertyType == typeof(long) ||
            propertyType == typeof(short) ||
            propertyType == typeof(uint) ||
            propertyType == typeof(ulong) ||
            propertyType == typeof(ushort) ||
            propertyType == typeof(byte) ||
            propertyType == typeof(sbyte))
            return true;
        Console.WriteLine(
            $"[!!] Encountered number for {field.Name} in {containerType.Name}, the class defines {propertyType.Name}!");
        return false;
    }

    private static bool _checkDictionary(this JsonProperty field, Type containerType, Type propertyType) {
        var keyType = propertyType.GetGenericArguments()[0];
        var valueType = propertyType.GetGenericArguments()[1];
        valueType = Nullable.GetUnderlyingType(valueType) ?? valueType;
        Console.WriteLine(
            $"Encountered dictionary {field.Name} with key type {keyType.Name} and value type {valueType.Name}!");

        return field.Value.EnumerateObject()
            // TODO: use key.Value?
            .Where(key => !valueType.IsPrimitive && valueType != typeof(string))
            .Aggregate(false, (current, key) =>
                current | key.FindExtraJsonPropertyFieldsByValueKind(containerType, valueType)
            );
    }

    private static bool _checkList(this JsonProperty field, Type containerType, Type propertyType) {
        var valueType = propertyType.GetGenericArguments()[0];
        valueType = Nullable.GetUnderlyingType(valueType) ?? valueType;
        Console.WriteLine(
            $"Encountered list {field.Name} with value type {valueType.Name}!");

        return field.Value.EnumerateArray()
            .Where(key => !valueType.IsPrimitive && valueType != typeof(string))
            .Aggregate(false, (current, key) =>
                current | key.FindExtraJsonElementFields(valueType, field.Name)
            );
    }
}