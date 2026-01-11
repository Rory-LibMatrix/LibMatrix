using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ArcaneLibs.Extensions;
using LibMatrix.Abstractions;
using LibMatrix.Extensions;
using LibMatrix.Responses.Federation;
using Microsoft.Extensions.Primitives;

namespace LibMatrix.Federation;

public class XMatrixAuthorizationScheme {
    public class XMatrixAuthorizationHeader {
        public const string Scheme = "X-Matrix";

        [JsonPropertyName("origin")]
        public required string Origin { get; set; }

        [JsonPropertyName("destination")]
        public required string Destination { get; set; }

        [JsonPropertyName("key")]
        public required string Key { get; set; }

        [JsonPropertyName("sig")]
        public required string Signature { get; set; }

        public static XMatrixAuthorizationHeader FromHeaderValue(AuthenticationHeaderValue header) {
            if (header.Scheme != Scheme)
                throw new LibMatrixException() {
                    Error = $"Expected authentication scheme of {Scheme}, got {header.Scheme}",
                    ErrorCode = MatrixException.ErrorCodes.M_UNAUTHORIZED
                };

            if (string.IsNullOrWhiteSpace(header.Parameter))
                throw new LibMatrixException() {
                    Error = $"Expected authentication header to have a value.",
                    ErrorCode = MatrixException.ErrorCodes.M_UNAUTHORIZED
                };

            var headerValues = new Dictionary<string, string>();
            var parts = header.Parameter.Split(',');
            foreach (var part in parts) {
                var kv = part.Split('=', 2);
                if (kv.Length != 2)
                    continue;
                var key = kv[0].Trim();
                var value = kv[1].Trim().Trim('"');
                headerValues[key] = value;
            }

            Console.WriteLine("X-Matrix parts: " + headerValues.ToJson(unsafeContent: true));

            var xma = new XMatrixAuthorizationHeader() {
                Destination = headerValues["destination"],
                Key = headerValues["key"],
                Origin = headerValues["origin"],
                Signature = headerValues["sig"]
            };
            Console.WriteLine("Parsed X-Matrix Auth Header: " + xma.ToJson());
            return xma;
        }

        public static XMatrixAuthorizationHeader FromSignedObject(SignedObject<XMatrixRequestSignature> signedObj, VersionedHomeserverPrivateKey currentKey) =>
            new() {
                Origin = signedObj.TypedContent.OriginServerName,
                Destination = signedObj.TypedContent.DestinationServerName,
                Signature = signedObj.Signatures[signedObj.TypedContent.OriginServerName][currentKey.KeyId],
                Key = currentKey.KeyId
            };

        public string ToHeaderValue() => $"{Scheme} origin=\"{Origin}\", destination=\"{Destination}\", key=\"{Key}\", sig=\"{Signature}\"";
    }

    public class XMatrixRequestSignature {
        [JsonPropertyName("method")]
        public required string Method { get; set; }

        [JsonPropertyName("uri")]
        public required string Uri { get; set; }

        [JsonPropertyName("origin")]
        public required string OriginServerName { get; set; }

        [JsonPropertyName("destination")]
        public required string DestinationServerName { get; set; }

        [JsonPropertyName("content"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public JsonObject? Content { get; set; }
    }
}