using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ArcaneLibs.Extensions;
using LibMatrix.Abstractions;
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

            var headerValues = new StringValues(header.Parameter);
            foreach (var value in headerValues) {
                Console.WriteLine(headerValues.ToJson());
            }

            return new() {
                Destination = "",
                Key = "",
                Origin = "",
                Signature = ""
            };
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

        [JsonPropertyName("content"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public JsonObject? Content { get; set; }
    }
}