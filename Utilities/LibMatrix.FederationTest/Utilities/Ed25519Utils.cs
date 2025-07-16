using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

namespace LibMatrix.FederationTest.Utilities;

public class Ed25519Utils {
    public static (Ed25519PrivateKeyParameters privateKey, Ed25519PublicKeyParameters publicKey) GenerateKeyPair() {
        Console.WriteLine("Generating new Ed25519 key pair!");
        var keyPairGen = new Ed25519KeyPairGenerator();
        keyPairGen.Init(new Ed25519KeyGenerationParameters(new SecureRandom()));
        var keyPair = keyPairGen.GenerateKeyPair();

        var privateKey = (Ed25519PrivateKeyParameters)keyPair.Private;
        var publicKey = (Ed25519PublicKeyParameters)keyPair.Public;
        
        return (privateKey, publicKey);
    }
    
    public static Ed25519PublicKeyParameters LoadPublicKeyFromEncoded(string key) {
        var keyBytes = UnpaddedBase64.Decode(key);
        return new Ed25519PublicKeyParameters(keyBytes, 0);
    }
    
    public static Ed25519PrivateKeyParameters LoadPrivateKeyFromEncoded(byte[] key) {
        return new Ed25519PrivateKeyParameters(key, 0);
    }
}