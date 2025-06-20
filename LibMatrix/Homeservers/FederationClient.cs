using System.Text.Json.Serialization;
using LibMatrix.Extensions;
using LibMatrix.Services;
using Microsoft.VisualBasic.CompilerServices;

namespace LibMatrix.Homeservers;

public class FederationClient {
    public FederationClient(string federationEndpoint, string? proxy = null) {
        HttpClient = new MatrixHttpClient {
            BaseAddress = new Uri(proxy?.TrimEnd('/') ?? federationEndpoint.TrimEnd('/')),
            // Timeout = TimeSpan.FromSeconds(120) // TODO: Re-implement this
        };
        if (proxy is not null) HttpClient.DefaultRequestHeaders.Add("MXAE_UPSTREAM", federationEndpoint);
    }

    public MatrixHttpClient HttpClient { get; set; }
    public HomeserverResolverService.WellKnownUris WellKnownUris { get; set; }

    public async Task<ServerVersionResponse> GetServerVersionAsync() => await HttpClient.GetFromJsonAsync<ServerVersionResponse>("/_matrix/federation/v1/version");
    public async Task<ServerKeysResponse> GetServerKeysAsync() => await HttpClient.GetFromJsonAsync<ServerKeysResponse>("/_matrix/key/v2/server");
}

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

    public class VersionedKeyId {
        public required string Algorithm { get; set; }
        public required string KeyId { get; set; }

        public static implicit operator VersionedKeyId(string key) {
            var parts = key.Split(':', 2);
            if (parts.Length != 2) throw new ArgumentException("Invalid key format. Expected 'algorithm:keyId'.", nameof(key));
            return new VersionedKeyId { Algorithm = parts[0], KeyId = parts[1] };
        }

        public static implicit operator string(VersionedKeyId key) => $"{key.Algorithm}:{key.KeyId}";
        public static implicit operator (string, string)(VersionedKeyId key) => (key.Algorithm, key.KeyId);
        public static implicit operator VersionedKeyId((string algorithm, string keyId) key) => (key.algorithm, key.keyId);
    }

    public class CurrentVerifyKey {
        [JsonPropertyName("key")]
        public string Key { get; set; }
    }

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

public class ServerVersionResponse {
    [JsonPropertyName("server")]
    public required ServerInfo Server { get; set; }

    public class ServerInfo {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("version")]
        public string Version { get; set; }
    }
}