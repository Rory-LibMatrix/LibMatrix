using System.Text.Json.Serialization;
using ArcaneLibs.Extensions;

namespace LibMatrix;

public class MatrixException : Exception {
    [JsonPropertyName("errcode")]
    public required string ErrorCode { get; set; }

    [JsonPropertyName("error")]
    public required string Error { get; set; }

    [JsonPropertyName("soft_logout")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? SoftLogout { get; set; }

    [JsonPropertyName("retry_after_ms")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? RetryAfterMs { get; set; }

    public string RawContent { get; set; }

    public object GetAsObject() => new { errcode = ErrorCode, error = Error, soft_logout = SoftLogout, retry_after_ms = RetryAfterMs };
    public string GetAsJson() => GetAsObject().ToJson(ignoreNull: true);

    public override string Message =>
        $"{ErrorCode}: " +
        (!string.IsNullOrWhiteSpace(Error)
            ? Error
            : ErrorCode switch {
                // common
                "M_FORBIDDEN" => $"You do not have permission to perform this action: {Error}",
                "M_UNKNOWN_TOKEN" => $"The access token specified was not recognised: {Error}{(SoftLogout == true ? " (soft logout)" : "")}",
                "M_MISSING_TOKEN" => $"No access token was specified: {Error}",
                "M_BAD_JSON" => $"Request contained valid JSON, but it was malformed in some way: {Error}",
                "M_NOT_JSON" => $"Request did not contain valid JSON: {Error}",
                "M_NOT_FOUND" => $"The requested resource was not found: {Error}",
                "M_LIMIT_EXCEEDED" => $"Too many requests have been sent in a short period of time. Wait a while then try again: {Error}",
                "M_UNRECOGNISED" => $"The server did not recognise the request: {Error}",
                "M_UNKOWN" => $"The server encountered an unexpected error: {Error}",
                // endpoint specific
                "M_UNAUTHORIZED" => $"The request did not contain valid authentication information for the target of the request: {Error}",
                "M_USER_DEACTIVATED" => $"The user ID associated with the request has been deactivated: {Error}",
                "M_USER_IN_USE" => $"The user ID associated with the request is already in use: {Error}",
                "M_INVALID_USERNAME" => $"The requested user ID is not valid: {Error}",
                "M_ROOM_IN_USE" => $"The room alias requested is already taken: {Error}",
                "M_INVALID_ROOM_STATE" => $"The room associated with the request is not in a valid state to perform the request: {Error}",
                "M_THREEPID_IN_USE" => $"The threepid requested is already associated with a user ID on this server: {Error}",
                "M_THREEPID_NOT_FOUND" => $"The threepid requested is not associated with any user ID: {Error}",
                "M_THREEPID_AUTH_FAILED" => $"The provided threepid and/or token was invalid: {Error}",
                "M_THREEPID_DENIED" => $"The homeserver does not permit the third party identifier in question: {Error}",
                "M_SERVER_NOT_TRUSTED" => $"The homeserver does not trust the identity server: {Error}",
                "M_UNSUPPORTED_ROOM_VERSION" => $"The room version is not supported: {Error}",
                "M_INCOMPATIBLE_ROOM_VERSION" => $"The room version is incompatible: {Error}",
                "M_BAD_STATE" => $"The request was invalid because the state was invalid: {Error}",
                "M_GUEST_ACCESS_FORBIDDEN" => $"Guest access is forbidden: {Error}",
                "M_CAPTCHA_NEEDED" => $"Captcha needed: {Error}",
                "M_CAPTCHA_INVALID" => $"Captcha invalid: {Error}",
                "M_MISSING_PARAM" => $"Missing parameter: {Error}",
                "M_INVALID_PARAM" => $"Invalid parameter: {Error}",
                "M_TOO_LARGE" => $"The request or entity was too large: {Error}",
                "M_EXCLUSIVE" =>
                    $"The resource being requested is reserved by an application service, or the application service making the request has not created the resource: {Error}",
                "M_RESOURCE_LIMIT_EXCEEDED" => $"Exceeded resource limit: {Error}",
                "M_CANNOT_LEAVE_SERVER_NOTICE_ROOM" => $"Cannot leave server notice room: {Error}",
                _ => $"Unknown error: {new { ErrorCode, Error, SoftLogout, RetryAfterMs }.ToJson(ignoreNull: true)}"
            });

    public static class ErrorCodes {
        public const string M_FORBIDDEN = "M_FORBIDDEN";
        public const string M_UNKNOWN_TOKEN = "M_UNKNOWN_TOKEN";
        public const string M_MISSING_TOKEN = "M_MISSING_TOKEN";
        public const string M_BAD_JSON = "M_BAD_JSON";
        public const string M_NOT_JSON = "M_NOT_JSON";
        public const string M_NOT_FOUND = "M_NOT_FOUND";
        public const string M_LIMIT_EXCEEDED = "M_LIMIT_EXCEEDED";
        public const string M_UNRECOGNISED = "M_UNRECOGNISED";
        public const string M_UNKOWN = "M_UNKOWN";
        public const string M_UNAUTHORIZED = "M_UNAUTHORIZED";
        public const string M_USER_DEACTIVATED = "M_USER_DEACTIVATED";
        public const string M_USER_IN_USE = "M_USER_IN_USE";
        public const string M_INVALID_USERNAME = "M_INVALID_USERNAME";
        public const string M_ROOM_IN_USE = "M_ROOM_IN_USE";
        public const string M_INVALID_ROOM_STATE = "M_INVALID_ROOM_STATE";
        public const string M_THREEPID_IN_USE = "M_THREEPID_IN_USE";
        public const string M_THREEPID_NOT_FOUND = "M_THREEPID_NOT_FOUND";
        public const string M_THREEPID_AUTH_FAILED = "M_THREEPID_AUTH_FAILED";
        public const string M_THREEPID_DENIED = "M_THREEPID_DENIED";
        public const string M_SERVER_NOT_TRUSTED = "M_SERVER_NOT_TRUSTED";
        public const string M_UNSUPPORTED_ROOM_VERSION = "M_UNSUPPORTED_ROOM_VERSION";
        public const string M_INCOMPATIBLE_ROOM_VERSION = "M_INCOMPATIBLE_ROOM_VERSION";
        public const string M_BAD_STATE = "M_BAD_STATE";
        public const string M_GUEST_ACCESS_FORBIDDEN = "M_GUEST_ACCESS_FORBIDDEN";
        public const string M_CAPTCHA_NEEDED = "M_CAPTCHA_NEEDED";
        public const string M_CAPTCHA_INVALID = "M_CAPTCHA_INVALID";
        public const string M_MISSING_PARAM = "M_MISSING_PARAM";
        public const string M_INVALID_PARAM = "M_INVALID_PARAM";
        public const string M_TOO_LARGE = "M_TOO_LARGE";
        public const string M_EXCLUSIVE = "M_EXCLUSIVE";
        public const string M_RESOURCE_LIMIT_EXCEEDED = "M_RESOURCE_LIMIT_EXCEEDED";
        public const string M_CANNOT_LEAVE_SERVER_NOTICE_ROOM = "M_CANNOT_LEAVE_SERVER_NOTICE_ROOM";
    }
}