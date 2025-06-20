using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using ArcaneLibs.Extensions;
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
            foreach (var value in headerValues)
            {
                Console.WriteLine(headerValues.ToJson());
            }

            return new() {
                Destination = "",
                Key = "",
                Origin = "",
                Signature = ""
            };
        }

        public string ToHeaderValue() => $"{Scheme} origin=\"{Origin}\", destination=\"{Destination}\", key=\"{Key}\", sig=\"{Signature}\"";
    }
}