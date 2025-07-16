using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace LibMatrix.Abstractions;

[DebuggerDisplay("{Algorithm}:{KeyId}")]
[SuppressMessage("ReSharper", "NonReadonlyMemberInGetHashCode")]
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

    public override bool Equals(object? obj) => obj is VersionedKeyId other && Algorithm == other.Algorithm && KeyId == other.KeyId;
    public override int GetHashCode() => HashCode.Combine(Algorithm, KeyId);
}