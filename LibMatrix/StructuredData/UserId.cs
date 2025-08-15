namespace LibMatrix.StructuredData;

public class UserId {
    public required string ServerName { get; set; }
    public required string LocalPart { get; set; }

    public static UserId Parse(string mxid) {
        if (!mxid.StartsWith('@')) throw new ArgumentException("Matrix User IDs must start with '@'", nameof(mxid));
        var parts = mxid.Split(':', 2);
        if (parts.Length != 2) throw new ArgumentException($"Invalid MXID '{mxid}' passed! MXIDs must exist of only 2 parts!", nameof(mxid));
        return new UserId {
            LocalPart = parts[0][1..],
            ServerName = parts[1]
        };
    }

    public static implicit operator UserId(string mxid) => Parse(mxid);
    public static implicit operator string(UserId mxid) => $"@{mxid.LocalPart}:{mxid.ServerName}";
    public static implicit operator (string, string)(UserId mxid) => (mxid.LocalPart, mxid.ServerName);
    public static implicit operator UserId((string localPart, string serverName) mxid) => (mxid.localPart, mxid.serverName);
    // public override string ToString() => $"mxc://{ServerName}/{MediaId}";

    public void Deconstruct(out string serverName, out string localPart) {
        serverName = ServerName;
        localPart = LocalPart;
    }
}