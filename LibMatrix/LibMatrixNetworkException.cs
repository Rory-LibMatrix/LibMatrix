using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using ArcaneLibs.Extensions;
// ReSharper disable MemberCanBePrivate.Global

namespace LibMatrix;

public class LibMatrixNetworkException : Exception {
    public LibMatrixNetworkException() : base() { }
    public LibMatrixNetworkException(Exception httpRequestException) : base("A network error occurred", httpRequestException) { }

    [JsonPropertyName("errcode")]
    public required string ErrorCode { get; set; }

    [JsonPropertyName("error")]
    public required string Error { get; set; }
    
    public object GetAsObject() => new { errcode = ErrorCode, error = Error };
    public string GetAsJson() => GetAsObject().ToJson(ignoreNull: true);

    public override string Message =>
        $"{ErrorCode}: {ErrorCode switch {
            ErrorCodes.RLM_NET_UNKNOWN_HOST => "The specified host could not be found.",
            ErrorCodes.RLM_NET_INVALID_REMOTE_CERTIFICATE => "The remote server's TLS certificate is invalid or could not be verified.",
            _ => $"Unknown error: {GetAsObject().ToJson(ignoreNull: true)}"
        }}\nError: {Error}";

    [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Follows spec naming")]
    public static class ErrorCodes {
        public const string RLM_NET_UNKNOWN_HOST = "RLM_NET_UNKNOWN_HOST";
        public const string RLM_NET_INVALID_REMOTE_CERTIFICATE = "RLM_NET_INVALID_REMOTE_CERTIFICATE";
    }
}