using System.Text.Json;
using System.Text.Json.Nodes;
using LibMatrix.Abstractions;
using LibMatrix.Extensions;
using LibMatrix.FederationTest.Utilities;
using LibMatrix.Responses.Federation;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math.EC.Rfc8032;

namespace LibMatrix.Federation.Extensions;
public static class ObjectExtensions {
    public static SignedObject<T> Sign<T>(this T content, string serverName, string keyName, Ed25519PrivateKeyParameters key) {
        SignedObject<T> signedObject = new() {
            Signatures = [],
            Content = JsonSerializer.Deserialize<JsonObject>(JsonSerializer.Serialize(content)) ?? new JsonObject(),
        };

        var contentBytes = CanonicalJsonSerializer.SerializeToUtf8Bytes(signedObject.Content);
        var signature = new byte[Ed25519.SignatureSize];
        key.Sign(Ed25519.Algorithm.Ed25519, null, contentBytes, 0, contentBytes.Length, signature, 0);

        if (!signedObject.Signatures.ContainsKey(serverName))
            signedObject.Signatures[serverName] = new Dictionary<string, string>();

        signedObject.Signatures[serverName][keyName] = UnpaddedBase64.Encode(signature);
        return signedObject;
    }

    public static SignedObject<T> Sign<T>(this T content, VersionedHomeserverPrivateKey privateKey) 
        => Sign(content, privateKey.ServerName, privateKey.KeyId, privateKey.GetPrivateEd25519Key());
}