using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using LibMatrix.HomeserverEmulator.Services;
using LibMatrix.Services;
using Microsoft.AspNetCore.Mvc;

namespace LibMatrix.HomeserverEmulator.Controllers.Media;

[ApiController]
[Route("/_matrix/media/{version}/")]
public class MediaController(
    ILogger<MediaController> logger,
    TokenService tokenService,
    UserStore userStore,
    HSEConfiguration cfg,
    HomeserverResolverService hsResolver,
    MediaStore mediaStore)
    : ControllerBase {
    [HttpPost("upload")]
    public async Task<object> UploadMedia([FromHeader(Name = "Content-Type")] string ContentType, [FromQuery] string filename, [FromBody] Stream file) {
        var token = tokenService.GetAccessTokenOrNull(HttpContext);
        if (token == null)
            throw new MatrixException() {
                ErrorCode = "M_MISSING_TOKEN",
                Error = "Missing token"
            };

        var user = await userStore.GetUserByToken(token);
        if (user == null)
            throw new MatrixException() {
                ErrorCode = "M_UNKNOWN_TOKEN",
                Error = "No such user"
            };

        var mediaId = Guid.NewGuid().ToString();
        var media = new {
            content_uri = $"mxc://{tokenService.GenerateServerName(HttpContext)}/{mediaId}"
        };
        return media;
    }

    [HttpGet("download/{serverName}/{mediaId}")]
    public async Task DownloadMedia(string serverName, string mediaId) {
        Response.Headers["Access-Control-Allow-Origin"] = "*";
        Response.Headers["Access-Control-Allow-Methods"] = "GET";
        
        var stream = await DownloadRemoteMedia(serverName, mediaId);
        await stream.CopyToAsync(Response.Body);
    }

    [HttpGet("thumbnail/{serverName}/{mediaId}")]
    public async Task DownloadThumbnail(string serverName, string mediaId) {
        await DownloadMedia(serverName, mediaId);
    }

    [HttpGet("preview_url")]
    public async Task<JsonObject> GetPreviewUrl([FromQuery] string url) {
        JsonObject data = new();

        using var hc = new HttpClient();
        logger.LogInformation("Getting URL preview for {}", url);
        using var response = await hc.GetAsync(url);
        var doc = await response.Content.ReadAsStringAsync();
        var match = Regex.Match(doc, "<meta property=\"(.*?)\" content=\"(.*?)\"");

        while (match.Success) {
            data[match.Groups[1].Value] = match.Groups[2].Value;
            match = match.NextMatch();
        }

        return data;
    }

    private async Task<Stream> DownloadRemoteMedia(string serverName, string mediaId) {
        if (cfg.StoreData) {
            var path = Path.Combine(cfg.DataStoragePath, "media", serverName, mediaId);
            if (!System.IO.File.Exists(path)) {
                var mediaUrl = await hsResolver.ResolveMediaUri(serverName, $"mxc://{serverName}/{mediaId}");
                if (mediaUrl is null)
                    throw new MatrixException() {
                        ErrorCode = "M_NOT_FOUND",
                        Error = "Media not found"
                    };
                using var client = new HttpClient();
                var stream = await client.GetStreamAsync(mediaUrl);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await using var fs = System.IO.File.Create(path);
                await stream.CopyToAsync(fs);
            }
            return new FileStream(path, FileMode.Open);
        }
        else {
            var mediaUrl = await hsResolver.ResolveMediaUri(serverName, $"mxc://{serverName}/{mediaId}");
            if (mediaUrl is null)
                throw new MatrixException() {
                    ErrorCode = "M_NOT_FOUND",
                    Error = "Media not found"
                };
            using var client = new HttpClient();
            return await client.GetStreamAsync(mediaUrl);
        }
    }
}