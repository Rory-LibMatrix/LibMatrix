using LibMatrix.Abstractions;
using LibMatrix.FederationTest.Utilities;
using Org.BouncyCastle.Crypto.Parameters;

namespace LibMatrix.Federation.Extensions;

public static class Ed25519Extensions {
    public static string ToUnpaddedBase64(this Ed25519PublicKeyParameters key) => UnpaddedBase64.Encode(key.GetEncoded());
    public static string ToUnpaddedBase64(this Ed25519PrivateKeyParameters key) => UnpaddedBase64.Encode(key.GetEncoded());
    public static Ed25519PrivateKeyParameters GetPrivateEd25519Key(this VersionedHomeserverPrivateKey key) => new(UnpaddedBase64.Decode(key.PrivateKey), 0);
}