using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ArcaneLibs.Extensions;

namespace LibMatrix.Homeservers.ImplementationDetails.Synapse.Models.Responses;

public class SynapseNextTokenTotalCollectionResult {
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("next_token")]
    public string? NextToken { get; set; }
}

// [JsonConverter(typeof(SynapseCollectionJsonConverter<>))]
public class SynapseCollectionResult<T>(string chunkKey = "chunk", string prevTokenKey = "prev_token", string nextTokenKey = "next_token", string totalKey = "total") {
    public int? Total { get; set; }
    public string? PrevToken { get; set; }
    public string? NextToken { get; set; }
    public List<T> Chunk { get; set; } = [];

    // TODO: figure out how to provide an IAsyncEnumerable<T> for this
    // https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/use-utf8jsonreader#read-from-a-stream-using-utf8jsonreader

    // public async IAsyncEnumerable<T> FromJsonAsync(Stream stream) {
    //
    // }

    public SynapseCollectionResult<T> FromJson(Stream stream, Action<T> action) {
        byte[] buffer = new byte[4096];
        _ = stream.Read(buffer);
        var reader = new Utf8JsonReader(buffer, isFinalBlock: false, state: default);

        try {
            FromJsonInternal(stream, ref buffer, ref reader, action);
        }
        catch (JsonException e) {
            Console.WriteLine($"Caught a JsonException: {e}");
            int hexdumpWidth = 64;
            Console.WriteLine($"Check hexdump line {reader.BytesConsumed / hexdumpWidth} index {reader.BytesConsumed % hexdumpWidth}");
            buffer.HexDump(64);
        }
        finally { }

        return this;
    }

    private void FromJsonInternal(Stream stream, ref byte[] buffer, ref Utf8JsonReader reader, Action<T> action) {
        while (!reader.IsFinalBlock) {
            while (!reader.Read()) {
                GetMoreBytesFromStream(stream, ref buffer, ref reader);
            }

            if (reader.TokenType == JsonTokenType.PropertyName) {
                var propName = reader.GetString();
                Console.WriteLine($"SynapseCollectionResult: encountered property name: {propName}");

                while (!reader.Read()) {
                    GetMoreBytesFromStream(stream, ref buffer, ref reader);
                }

                Console.WriteLine($"{reader.BytesConsumed}/{stream.Position} {reader.TokenType}");

                if (propName == totalKey && reader.TokenType == JsonTokenType.Number) {
                    Total = reader.GetInt32();
                }
                else if (propName == prevTokenKey && reader.TokenType == JsonTokenType.String) {
                    PrevToken = reader.GetString();
                }
                else if (propName == nextTokenKey && reader.TokenType == JsonTokenType.String) {
                    NextToken = reader.GetString();
                }
                else if (propName == chunkKey) {
                    if (reader.TokenType == JsonTokenType.StartArray) {
                        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray) {
                            // if (reader.TokenType == JsonTokenType.EndArray) {
                            // break;
                            // }
                            // Console.WriteLine($"Encountered token in chunk: {reader.TokenType}");
                            // var _buf = reader.ValueSequence.ToArray();
                            // try {
                            //     var item = JsonSerializer.Deserialize<T>(_buf);
                            //     action(item);
                            //     Chunk.Add(item);
                            // }
                            // catch(JsonException e) {
                            //     Console.WriteLine($"Caught a JsonException: {e}");
                            //     int hexdumpWidth = 64;
                            //
                            //     // Console.WriteLine($"Check hexdump line {reader.BytesConsumed / hexdumpWidth} index {reader.BytesConsumed % hexdumpWidth}");
                            //     Console.WriteLine($"Buffer length: {_buf.Length}");
                            //     _buf.HexDump(64);
                            //     throw;
                            // }
                            var item = ReadItem(stream, ref buffer, ref reader);
                            action(item);
                            Chunk.Add(item);
                        }
                    }
                }
            }
        }
    }

    private T ReadItem(Stream stream, ref byte[] buffer, ref Utf8JsonReader reader) {
        while (!reader.Read()) {
            GetMoreBytesFromStream(stream, ref buffer, ref reader);
        }

        // handle nullable types
        if (typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(Nullable<>)) {
            if (reader.TokenType == JsonTokenType.Null) {
                return default(T);
            }
        }

        // if(typeof(T) == typeof(string)) {
        //     return (T)(object)reader.GetString();
        // }
        // else if(typeof(T) == typeof(int)) {
        //     return (T)(object)reader.GetInt32();
        // }
        // else {
        //     var _buf = reader.ValueSequence.ToArray();
        //     return JsonSerializer.Deserialize<T>(_buf);
        // }

        // default branch uses "object?" cast to avoid compiler error
        // add more branches here as nessesary
        // reader.Read();
        var call = typeof(T) switch {
            Type t when t == typeof(string) => reader.GetString(),
            _ => ReadObject<T>(stream, ref buffer, ref reader)
        };

        object ReadObject<T>(Stream stream, ref byte[] buffer, ref Utf8JsonReader reader) {
            if (reader.TokenType != JsonTokenType.PropertyName) {
                throw new JsonException();
            }

            List<byte> objBuffer = [(byte)'{', ..reader.ValueSequence.ToArray()];
            var currentDepth = reader.CurrentDepth;
            while (reader.CurrentDepth >= currentDepth) {
                while (!reader.Read()) {
                    GetMoreBytesFromStream(stream, ref buffer, ref reader);
                }

                if (reader.TokenType == JsonTokenType.EndObject && reader.CurrentDepth == currentDepth) {
                    break;
                }

                objBuffer.AddRange(reader.ValueSpan);
            }

            return JsonSerializer.Deserialize<T>(objBuffer.ToArray());
        }

        return (T)call;

        // return JsonSerializer.Deserialize<T>(ref reader);
    }

    private static void GetMoreBytesFromStream(Stream stream, ref byte[] buffer, ref Utf8JsonReader reader) {
        int bytesRead;
        if (reader.BytesConsumed < buffer.Length) {
            ReadOnlySpan<byte> leftover = buffer.AsSpan((int)reader.BytesConsumed);

            if (leftover.Length == buffer.Length) {
                Array.Resize(ref buffer, buffer.Length * 2);
                Console.WriteLine($"Increased buffer size to {buffer.Length}");
            }

            leftover.CopyTo(buffer);
            bytesRead = stream.Read(buffer.AsSpan(leftover.Length));
        }
        else {
            bytesRead = stream.Read(buffer);
        }

        // Console.WriteLine($"String in buffer is: {Encoding.UTF8.GetString(buffer)}");
        reader = new Utf8JsonReader(buffer, isFinalBlock: bytesRead == 0, reader.CurrentState);
    }
}

public partial class SynapseCollectionJsonConverter<T> : JsonConverter<SynapseCollectionResult<T>> {
    public override SynapseCollectionResult<T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if (reader.TokenType != JsonTokenType.StartObject) {
            throw new JsonException();
        }

        var result = new SynapseCollectionResult<T>();
        while (reader.Read()) {
            if (reader.TokenType == JsonTokenType.EndObject) {
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName) {
                throw new JsonException();
            }

            var propName = reader.GetString();
            reader.Read();
            if (propName == "total") {
                result.Total = reader.GetInt32();
            }
            else if (propName == "prev_token") {
                result.PrevToken = reader.GetString();
            }
            else if (propName == "next_token") {
                result.NextToken = reader.GetString();
            }
            else if (propName == "chunk") {
                if (reader.TokenType != JsonTokenType.StartArray) {
                    throw new JsonException();
                }

                while (reader.Read()) {
                    if (reader.TokenType == JsonTokenType.EndArray) {
                        break;
                    }

                    var item = JsonSerializer.Deserialize<T>(ref reader, options);
                    result.Chunk.Add(item);
                }
            }
        }

        return result;
    }

    public override void Write(Utf8JsonWriter writer, SynapseCollectionResult<T> value, JsonSerializerOptions options) {
        writer.WriteStartObject();
        if (value.Total is not null)
            writer.WriteNumber("total", value.Total ?? 0);
        if (value.PrevToken is not null)
            writer.WriteString("prev_token", value.PrevToken);
        if (value.NextToken is not null)
            writer.WriteString("next_token", value.NextToken);

        writer.WriteStartArray("chunk");
        foreach (var item in value.Chunk) {
            JsonSerializer.Serialize(writer, item, options);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}