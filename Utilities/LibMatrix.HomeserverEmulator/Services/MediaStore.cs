using System.Text.Json;
using LibMatrix.Services;

namespace LibMatrix.HomeserverEmulator.Services;

public class MediaStore {
    private readonly HseConfiguration _config;
    private readonly HomeserverResolverService _hsResolver;
    private List<MediaInfo> _mediaIndex = new();

    public MediaStore(HseConfiguration config, HomeserverResolverService hsResolver) {
        _config = config;
        _hsResolver = hsResolver;
        if (config.StoreData) {
            var path = Path.Combine(config.DataStoragePath, "media");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            if (File.Exists(Path.Combine(path, "index.json")))
                _mediaIndex = JsonSerializer.Deserialize<List<MediaInfo>>(File.ReadAllText(Path.Combine(path, "index.json")));
        }
        else
            Console.WriteLine("Data storage is disabled, not loading rooms from disk");
    }

    // public async Task<object> UploadMedia(string userId, string mimeType, Stream stream, string? filename = null) {
        // var mediaId = $"mxc://{Guid.NewGuid().ToString()}";
        // var path = Path.Combine(_config.DataStoragePath, "media", mediaId);
        // if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        // var file = Path.Combine(path, filename ?? "file");
        // await using var fs = File.Create(file);
        // await stream.CopyToAsync(fs);
        // index.Add(new() { });
        // return media;
    // }

    public async Task<Stream> GetRemoteMedia(string serverName, string mediaId) {
        if (_config.StoreData) {
            var path = Path.Combine(_config.DataStoragePath, "media", serverName, mediaId);
            if (!File.Exists(path)) {
                // var mediaUrl = await _hsResolver.ResolveMediaUri(serverName, $"mxc://{serverName}/{mediaId}");
                var homeserver = (await _hsResolver.ResolveHomeserverFromWellKnown(serverName)).Client;
                var mediaUrl = homeserver is null ? null : $"{homeserver}/_matrix/media/v3/download/";
                if (mediaUrl is null)
                    throw new MatrixException() {
                        ErrorCode = "M_NOT_FOUND",
                        Error = "Media not found"
                    };
                using var client = new HttpClient();
                var stream = await client.GetStreamAsync(mediaUrl);
                await using var fs = File.Create(path);
                await stream.CopyToAsync(fs);
            }
            return new FileStream(path, FileMode.Open);
        }
        else {
            // var mediaUrl = await _hsResolver.ResolveMediaUri(serverName, $"mxc://{serverName}/{mediaId}");
            var homeserver = (await _hsResolver.ResolveHomeserverFromWellKnown(serverName)).Client;
            var mediaUrl = homeserver is null ? null : $"{homeserver}/_matrix/media/v3/download/";
            if (mediaUrl is null)
                throw new MatrixException() {
                    ErrorCode = "M_NOT_FOUND",
                    Error = "Media not found"
                };
            using var client = new HttpClient();
            return await client.GetStreamAsync(mediaUrl);
        }
    }
    public class MediaInfo { }
}