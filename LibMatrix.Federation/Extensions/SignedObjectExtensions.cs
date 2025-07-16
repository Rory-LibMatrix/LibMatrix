using LibMatrix.Extensions;
using LibMatrix.FederationTest.Utilities;
using LibMatrix.Responses.Federation;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math.EC.Rfc8032;

namespace LibMatrix.Federation.Extensions;
public static class SignedObjectExtensions {
    public static SignedObject<T> Sign<T>(this SignedObject<T> content, string serverName, string keyName, Ed25519PrivateKeyParameters key) {
        var signResult = content.Content.Sign(serverName, keyName, key);
        var signedObject = new SignedObject<T> {
            Signatures = content.Signatures,
            Content = signResult.Content
        };
        
        if (!signedObject.Signatures.ContainsKey(serverName))
            signedObject.Signatures[serverName] = new Dictionary<string, string>();
        
        signedObject.Signatures[serverName][keyName] = signResult.Signatures[serverName][keyName];
        return signedObject;
    }

    public static bool ValidateSignature<T>(this SignedObject<T> content, string serverName, string keyName, Ed25519PublicKeyParameters key) {
        if (!content.Signatures.TryGetValue(serverName, out var serverSignatures))
            return false;

        if (!serverSignatures.TryGetValue(keyName, out var signatureBase64))
            return false;

        var signature = UnpaddedBase64.Decode(signatureBase64);
        if (signature.Length != Ed25519.SignatureSize)
            return false;

        var contentBytes = CanonicalJsonSerializer.SerializeToUtf8Bytes(content.Content);
        return Ed25519.Verify(signature, 0, key.GetEncoded(), 0, contentBytes, 0, contentBytes.Length);
    }
}