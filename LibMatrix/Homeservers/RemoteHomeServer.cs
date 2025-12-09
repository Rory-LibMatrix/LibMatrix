using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using ArcaneLibs.Collections;
using ArcaneLibs.Extensions;
using LibMatrix.Extensions;
using LibMatrix.Responses;
using LibMatrix.Services;

namespace LibMatrix.Homeservers;

public class RemoteHomeserver {
    public RemoteHomeserver(string serverName, HomeserverResolverService.WellKnownUris wellKnownUris, string? proxy) {
        if (string.IsNullOrWhiteSpace(proxy))
            proxy = null;
        ServerNameOrUrl = serverName;
        WellKnownUris = wellKnownUris;
        Proxy = proxy;
        ClientHttpClient = new MatrixHttpClient {
            BaseAddress = new Uri(proxy?.TrimEnd('/') ?? wellKnownUris.Client?.TrimEnd('/') ?? throw new InvalidOperationException($"No client URI for {serverName}!")),
            // Timeout = TimeSpan.FromSeconds(300) // TODO: Re-implement this
        };

        if (!string.IsNullOrWhiteSpace(wellKnownUris.Server))
            FederationClient = new FederationClient(WellKnownUris.Server!, proxy);
        Auth = new(this);
    }

    // private Dictionary<string, object> _profileCache { get; set; } = new();
    private SemaphoreCache<UserProfileResponse> _profileCache { get; set; } = new();
    public string ServerNameOrUrl { get; }
    public string? Proxy { get; }

    [JsonIgnore]
    public MatrixHttpClient ClientHttpClient { get; set; }

    [JsonIgnore]
    public FederationClient? FederationClient { get; set; }

    public HomeserverResolverService.WellKnownUris WellKnownUris { get; set; }

    // TODO: Do we need to support retrieving individual profile properties? Is there any use for that besides just getting the full profile?
    public async Task<UserProfileResponse> GetProfileAsync(string mxid) =>
        await ClientHttpClient.GetFromJsonAsync<UserProfileResponse>($"/_matrix/client/v3/profile/{HttpUtility.UrlEncode(mxid)}");

    public async Task<ClientVersionsResponse> GetClientVersionsAsync() {
        var resp = await ClientHttpClient.GetAsync("/_matrix/client/versions");
        var data = await resp.Content.ReadFromJsonAsync<ClientVersionsResponse>();
        if (!resp.IsSuccessStatusCode) Console.WriteLine("ClientVersions: " + data);
        return data ?? throw new InvalidOperationException("ClientVersionsResponse is null");
    }

    public async Task<AliasResult> ResolveRoomAliasAsync(string alias) {
        var resp = await ClientHttpClient.GetAsync($"/_matrix/client/v3/directory/room/{alias.Replace("#", "%23")}");
        var data = await resp.Content.ReadFromJsonAsync<AliasResult>();
        //var text = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode) Console.WriteLine("ResolveAlias: " + data.ToJson());
        return data ?? throw new InvalidOperationException($"Could not resolve alias {alias}");
    }

    public Task<PublicRoomDirectoryResult> GetPublicRoomsAsync(int limit = 100, string? server = null, string? since = null) {
        var url = $"/_matrix/client/v3/publicRooms?limit={limit}";
        if (!string.IsNullOrWhiteSpace(server)) {
            url += $"&server={server}";
        }

        if (!string.IsNullOrWhiteSpace(since)) {
            url += $"&since={since}";
        }

        return ClientHttpClient.GetFromJsonAsync<PublicRoomDirectoryResult>(url);
    }

    public async IAsyncEnumerable<PublicRoomDirectoryResult> EnumeratePublicRoomsAsync(int limit = int.MaxValue, string? server = null, string? since = null, int chunkSize = 100) {
        PublicRoomDirectoryResult res;
        do {
            res = await GetPublicRoomsAsync(chunkSize, server, since);
            yield return res;
            if (res.NextBatch is null || res.NextBatch == since || res.Chunk.Count == 0) break;
            since = res.NextBatch;
        } while (limit > 0 && limit-- > 0);
    }

    public async Task<RoomDirectoryVisibilityResponse> GetRoomDirectoryVisibilityAsync(string roomId)
        => await (await ClientHttpClient.GetAsync($"/_matrix/client/v3/directory/list/room/{HttpUtility.UrlEncode(roomId)}")).Content
            .ReadFromJsonAsync<RoomDirectoryVisibilityResponse>() ?? throw new InvalidOperationException();

#region Authentication

    public async Task<LoginResponse> LoginAsync(string username, string password, string? deviceName = null) {
        var resp = await ClientHttpClient.PostAsJsonAsync("/_matrix/client/r0/login", new {
            type = "m.login.password",
            identifier = new {
                type = "m.id.user",
                user = username
            },
            password,
            initial_device_display_name = deviceName
        });
        var data = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        return data ?? throw new InvalidOperationException("LoginResponse is null");
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
        return data ?? throw new InvalidOperationException("LoginResponse is null");
    }

#endregion

    public UserInteractiveAuthClient Auth;
}

public class RoomDirectoryVisibilityResponse {
    [JsonPropertyName("visibility")]
    public VisibilityValue Visibility { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum VisibilityValue {
        [JsonStringEnumMemberName("public")] Public,
        [JsonStringEnumMemberName("private")] Private
    }
}

public class PublicRoomDirectoryResult {
    [JsonPropertyName("chunk")]
    public List<PublicRoomListItem> Chunk { get; set; }

    [JsonPropertyName("next_batch")]
    public string? NextBatch { get; set; }

    [JsonPropertyName("prev_batch")]
    public string? PrevBatch { get; set; }

    [JsonPropertyName("total_room_count_estimate")]
    public int TotalRoomCountEstimate { get; set; }

    public class PublicRoomListItem {
        [JsonPropertyName("avatar_url")]
        public string? AvatarUrl { get; set; }

        [JsonPropertyName("canonical_alias")]
        public string? CanonicalAlias { get; set; }

        [JsonPropertyName("guest_can_join")]
        public bool GuestCanJoin { get; set; }

        [JsonPropertyName("join_rule")]
        public string JoinRule { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("num_joined_members")]
        public int NumJoinedMembers { get; set; }

        [JsonPropertyName("room_id")]
        public string RoomId { get; set; }

        [JsonPropertyName("topic")]
        public string? Topic { get; set; }

        [JsonPropertyName("world_readable")]
        public bool WorldReadable { get; set; }
    }
}

public class AliasResult {
    [JsonPropertyName("room_id")]
    public string RoomId { get; set; }

    [JsonPropertyName("servers")]
    public List<string> Servers { get; set; }
}