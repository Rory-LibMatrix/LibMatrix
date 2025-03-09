using System.Diagnostics.CodeAnalysis;

namespace LibMatrix;

public class MxcUri {
    public required string ServerName { get; set; }
    public required string MediaId { get; set; }

    public static MxcUri Parse([StringSyntax("Uri")] string mxcUri) {
        if (!mxcUri.StartsWith("mxc://")) throw new ArgumentException("Matrix Content URIs must start with 'mxc://'", nameof(mxcUri));
        var parts = mxcUri[6..].Split('/');
        if (parts.Length != 2) throw new ArgumentException($"Invalid Matrix Content URI '{mxcUri}' passed! Matrix Content URIs must exist of only 2 parts!", nameof(mxcUri));
        return new MxcUri {
            ServerName = parts[0],
            MediaId = parts[1]
        };
    }

    public static implicit operator MxcUri(string mxcUri) => Parse(mxcUri);
    public static implicit operator string(MxcUri mxcUri) => $"mxc://{mxcUri.ServerName}/{mxcUri.MediaId}";
    public static implicit operator (string, string)(MxcUri mxcUri) => (mxcUri.ServerName, mxcUri.MediaId);
    public static implicit operator MxcUri((string serverName, string mediaId) mxcUri) => (mxcUri.serverName, mxcUri.mediaId);
    // public override string ToString() => $"mxc://{ServerName}/{MediaId}";

    public string ToDownloadUri(string? baseUrl = null, string? filename = null, int? timeout = null) {
        var uri = $"{baseUrl}/_matrix/client/v1/media/download/{ServerName}/{MediaId}";
        if (filename is not null) uri += $"/{filename}";
        if (timeout is not null) uri += $"?timeout={timeout}";
        return uri;
    }

    public string ToLegacyDownloadUri(string? baseUrl = null, string? filename = null, int? timeout = null) {
        var uri = $"{baseUrl}/_matrix/media/v3/download/{ServerName}/{MediaId}";
        if (filename is not null) uri += $"/{filename}";
        if (timeout is not null) uri += $"?timeout_ms={timeout}";
        return uri;
    }

    public void Deconstruct(out string serverName, out string mediaId) {
        serverName = ServerName;
        mediaId = MediaId;
    }
}