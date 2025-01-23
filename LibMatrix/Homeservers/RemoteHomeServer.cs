using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using ArcaneLibs.Extensions;
using LibMatrix.Extensions;
using LibMatrix.Responses;
using LibMatrix.Services;

namespace LibMatrix.Homeservers;

public class RemoteHomeserver {
    public RemoteHomeserver(string baseUrl, HomeserverResolverService.WellKnownUris wellKnownUris, string? proxy) {
        if (string.IsNullOrWhiteSpace(proxy))
            proxy = null;
        BaseUrl = baseUrl;
        WellKnownUris = wellKnownUris;
        ClientHttpClient = new MatrixHttpClient {
            BaseAddress = new Uri(proxy?.TrimEnd('/') ?? wellKnownUris.Client?.TrimEnd('/') ?? throw new InvalidOperationException($"No client URI for {baseUrl}!")),
            // Timeout = TimeSpan.FromSeconds(300) // TODO: Re-implement this
        };

        if (proxy is not null) ClientHttpClient.DefaultRequestHeaders.Add("MXAE_UPSTREAM", baseUrl);
        if (!string.IsNullOrWhiteSpace(wellKnownUris.Server))
            FederationClient = new FederationClient(WellKnownUris.Server!, proxy);
        Auth = new(this);
    }

    private Dictionary<string, object> _profileCache { get; set; } = new();
    public string BaseUrl { get; }

    [JsonIgnore]
    public MatrixHttpClient ClientHttpClient { get; set; }

    [JsonIgnore]
    public FederationClient? FederationClient { get; set; }

    public HomeserverResolverService.WellKnownUris WellKnownUris { get; set; }

    public async Task<UserProfileResponse> GetProfileAsync(string mxid, bool useCache = false) {
        if (mxid is null) throw new ArgumentNullException(nameof(mxid));
        if (useCache && _profileCache.TryGetValue(mxid, out var value)) {
            if (value is SemaphoreSlim s) await s.WaitAsync();
            if (value is UserProfileResponse p) return p;
        }

        _profileCache[mxid] = new SemaphoreSlim(1);

        var resp = await ClientHttpClient.GetAsync($"/_matrix/client/v3/profile/{HttpUtility.UrlEncode(mxid)}");
        var data = await resp.Content.ReadFromJsonAsync<UserProfileResponse>();
        if (!resp.IsSuccessStatusCode) Console.WriteLine("Profile: " + data);
        _profileCache[mxid] = data;

        return data;
    }

    public async Task<ClientVersionsResponse> GetClientVersionsAsync() {
        var resp = await ClientHttpClient.GetAsync($"/_matrix/client/versions");
        var data = await resp.Content.ReadFromJsonAsync<ClientVersionsResponse>();
        if (!resp.IsSuccessStatusCode) Console.WriteLine("ClientVersions: " + data);
        return data;
    }

    public async Task<AliasResult> ResolveRoomAliasAsync(string alias) {
        var resp = await ClientHttpClient.GetAsync($"/_matrix/client/v3/directory/room/{alias.Replace("#", "%23")}");
        var data = await resp.Content.ReadFromJsonAsync<AliasResult>();
        //var text = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode) Console.WriteLine("ResolveAlias: " + data.ToJson());
        return data;
    }

#region Authentication

    public async Task<LoginResponse> LoginAsync(string username, string password, string? deviceName = null) {
        var resp = await ClientHttpClient.PostAsJsonAsync("/_matrix/client/r0/login", new {
            type = "m.login.password",
            identifier = new {
                type = "m.id.user",
                user = username
            },
            password = password,
            initial_device_display_name = deviceName
        });
        var data = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        if (!resp.IsSuccessStatusCode) Console.WriteLine("Login: " + data.ToJson());
        return data;
    }

    public async Task<LoginResponse> RegisterAsync(string username, string password, string? deviceName = null) {
        var resp = await ClientHttpClient.PostAsJsonAsync("/_matrix/client/r0/register", new {
            kind = "user",
            auth = new {
                type = "m.login.dummy"
            },
            username,
            password,
            initial_device_display_name = deviceName ?? "LibMatrix"
        }, new JsonSerializerOptions() {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
        var data = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        if (!resp.IsSuccessStatusCode) Console.WriteLine("Register: " + data.ToJson());
        return data;
    }

#endregion

    [Obsolete("This call uses the deprecated unauthenticated media endpoints, please switch to the relevant AuthenticatedHomeserver methods instead.", true)]
    public string? ResolveMediaUri(string? mxcUri) {
        if (mxcUri is null) return null;
        if (mxcUri.StartsWith("https://")) return mxcUri;
        return $"{ClientHttpClient.BaseAddress}/_matrix/media/v3/download/{mxcUri.Replace("mxc://", "")}".Replace("//_matrix", "/_matrix");
    }

    public UserInteractiveAuthClient Auth;
}

public class AliasResult {
    [JsonPropertyName("room_id")]
    public string RoomId { get; set; } = null!;

    [JsonPropertyName("servers")]
    public List<string> Servers { get; set; } = null!;
}