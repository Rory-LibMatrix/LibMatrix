using System.Diagnostics;
using System.Text.Json.Serialization;
using LibMatrix.Abstractions;

namespace LibMatrix.Responses.Federation;

public class ServerKeysResponse {
    [JsonPropertyName("server_name")]
    public string ServerName { get; set; }

    [JsonPropertyName("valid_until_ts")]
    public ulong ValidUntilTs { get; set; }

    [JsonIgnore]
    public DateTime ValidUntil {
        get => DateTimeOffset.FromUnixTimeMilliseconds((long)ValidUntilTs).DateTime;
        set => ValidUntilTs = (ulong)new DateTimeOffset(value).ToUnixTimeMilliseconds();
    }

    [JsonPropertyName("verify_keys")]
    public Dictionary<string, CurrentVerifyKey> VerifyKeys { get; set; } = new();

    [JsonIgnore]
    public Dictionary<VersionedKeyId, CurrentVerifyKey> VerifyKeysById {
        get => VerifyKeys.ToDictionary(key => (VersionedKeyId)key.Key, key => key.Value);
        set => VerifyKeys = value.ToDictionary(key => (string)key.Key, key => key.Value);
    }

    [JsonPropertyName("old_verify_keys")]
    public Dictionary<string, ExpiredVerifyKey> OldVerifyKeys { get; set; } = new();

    [JsonIgnore]
    public Dictionary<VersionedKeyId, ExpiredVerifyKey> OldVerifyKeysById {
        get => OldVerifyKeys.ToDictionary(key => (VersionedKeyId)key.Key, key => key.Value);
        set => OldVerifyKeys = value.ToDictionary(key => (string)key.Key, key => key.Value);
    }

    [DebuggerDisplay("{Key}")]
    public class CurrentVerifyKey {
        [JsonPropertyName("key")]
        public required string Key { get; set; }
    }

    [DebuggerDisplay("{Key} (expired {Expired})")]
    public class ExpiredVerifyKey : CurrentVerifyKey {
        [JsonPropertyName("expired_ts")]
        public ulong ExpiredTs { get; set; }

        [JsonIgnore]
        public DateTime Expired {
            get => DateTimeOffset.FromUnixTimeMilliseconds((long)ExpiredTs).DateTime;
            set => ExpiredTs = (ulong)new DateTimeOffset(value).ToUnixTimeMilliseconds();
        }
    }
}