using LibMatrix.FederationTest.Utilities;
using Org.BouncyCastle.Crypto.Parameters;

namespace LibMatrix.Federation.Extensions;

public static class Ed25519Extensions {
    public static string ToUnpaddedBase64(this Ed25519PublicKeyParameters key) => UnpaddedBase64.Encode(key.GetEncoded());
}