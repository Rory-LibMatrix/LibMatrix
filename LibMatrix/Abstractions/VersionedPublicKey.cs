namespace LibMatrix.Abstractions;

public class VersionedPublicKey {
    public required VersionedKeyId KeyId { get; set; }
    public required string PublicKey { get; set; }
}

public class VersionedPrivateKey : VersionedPublicKey {
    public required string PrivateKey { get; set; }
}
public class VersionedHomeserverPublicKey : VersionedPublicKey {
    public required string ServerName { get; set; }
}
public class VersionedHomeserverPrivateKey : VersionedPrivateKey {
    public required string ServerName { get; set; }
}