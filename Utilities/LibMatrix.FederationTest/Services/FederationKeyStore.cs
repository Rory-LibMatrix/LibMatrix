using System.Text.Json;
using LibMatrix.Abstractions;
using LibMatrix.FederationTest.Utilities;
using Org.BouncyCastle.Crypto.Parameters;

namespace LibMatrix.FederationTest.Services;

public class FederationKeyStore(FederationTestConfiguration config) {
    static FederationKeyStore() {
        Console.WriteLine("INFO | FederationKeyStore initialized.");
    }

    private static (Ed25519PrivateKeyParameters privateKey, Ed25519PublicKeyParameters publicKey) currentKeyPair = default;
    
    public class PrivateKeyCollection {
        
        public required VersionedHomeserverPrivateKey CurrentSigningKey { get; set; }
    }
    
    public PrivateKeyCollection GetCurrentSigningKey() {
        if(!Directory.Exists(config.KeyStorePath)) Directory.CreateDirectory(config.KeyStorePath);
        var privateKeyPath = Path.Combine(config.KeyStorePath, "private-keys.json");

        if (!File.Exists(privateKeyPath)) {
            var keyPair = InternalGetSigningKey();
            var privateKey = new VersionedHomeserverPrivateKey {
                PrivateKey = keyPair.privateKey.GetEncoded().ToUnpaddedBase64(),
            };
            File.WriteAllText(privateKeyPath, privateKey.ToJson());
        }
        
        return JsonSerializer.Deserialize<PrivateKeyCollection>()
    }

    private (Ed25519PrivateKeyParameters privateKey, Ed25519PublicKeyParameters publicKey) InternalGetSigningKey() {
        if (currentKeyPair != default) {
            return currentKeyPair;
        }
        
        if(!Directory.Exists(config.KeyStorePath)) Directory.CreateDirectory(config.KeyStorePath);
        
        var privateKeyPath = Path.Combine(config.KeyStorePath, "signing.key");
        if (!File.Exists(privateKeyPath)) {
            var keyPair = Ed25519Utils.GenerateKeyPair();
            File.WriteAllBytes(privateKeyPath, keyPair.privateKey.GetEncoded());
            return keyPair;
        }

        var privateKeyBytes = File.ReadAllBytes(privateKeyPath);
        var privateKey = Ed25519Utils.LoadPrivateKeyFromEncoded(privateKeyBytes);
        var publicKey = privateKey.GeneratePublicKey();
        return currentKeyPair = (privateKey, publicKey);
    }
}