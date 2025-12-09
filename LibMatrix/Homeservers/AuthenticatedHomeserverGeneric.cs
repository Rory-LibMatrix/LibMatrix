using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Web;
using ArcaneLibs.Collections;
using ArcaneLibs.Extensions;
using LibMatrix.EventTypes.Spec;
using LibMatrix.EventTypes.Spec.State.RoomInfo;
using LibMatrix.Filters;
using LibMatrix.Helpers;
using LibMatrix.Homeservers.Extensions.NamedCaches;
using LibMatrix.Responses;
using LibMatrix.RoomTypes;
using LibMatrix.Services;
using LibMatrix.StructuredData;
using LibMatrix.Utilities;

namespace LibMatrix.Homeservers;

public class AuthenticatedHomeserverGeneric : RemoteHomeserver {
    public AuthenticatedHomeserverGeneric(string serverName, HomeserverResolverService.WellKnownUris wellKnownUris, string? proxy, string accessToken) : base(serverName,
        wellKnownUris, proxy) {
        AccessToken = accessToken;
        ClientHttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        NamedCaches = new HsNamedCaches(this);
    }

    public async Task Initialise() {
        WhoAmI = await ClientHttpClient.GetFromJsonAsync<WhoAmIResponse>("/_matrix/client/v3/account/whoami");
    }

    private WhoAmIResponse? _whoAmI;

    public WhoAmIResponse WhoAmI {
        get => _whoAmI ?? throw new Exception("Initialise was not called or awaited, WhoAmI is null!");
        private set => _whoAmI = value;
    }

    public string UserId => WhoAmI.UserId;
    public string UserLocalpart => UserId.Split(":")[0][1..];
    public string ServerName => UserId.Split(":", 2)[1];
    public string BaseUrl => ClientHttpClient.BaseAddress!.ToString().TrimEnd('/');

    [JsonIgnore]
    public string AccessToken { get; set; }

    public HsNamedCaches NamedCaches { get; set; }

    public GenericRoom GetRoom(string roomId) {
        return new GenericRoom(this, roomId);
    }

    public virtual async Task<List<GenericRoom>> GetJoinedRooms() {
        var roomQuery = await ClientHttpClient.GetAsync("/_matrix/client/v3/joined_rooms");

        var roomsJson = await roomQuery.Content.ReadFromJsonAsync<JsonElement>();
        var rooms = roomsJson.GetProperty("joined_rooms").EnumerateArray().Select(room => GetRoom(room.GetString()!)).ToList();

        return rooms;
    }

    public virtual async Task<string> UploadFile(string fileName, IEnumerable<byte> data, string contentType = "application/octet-stream") {
        return await UploadFile(fileName, data.ToArray(), contentType);
    }

    public virtual async Task<string> UploadFile(string fileName, byte[] data, string contentType = "application/octet-stream") {
        await using var ms = new MemoryStream(data);
        return await UploadFile(fileName, ms, contentType);
    }

    public virtual async Task<string> UploadFile(string fileName, Stream fileStream, string contentType = "application/octet-stream") {
        var req = new HttpRequestMessage(HttpMethod.Post, $"/_matrix/media/v3/upload?filename={fileName}");
        req.Content = new StreamContent(fileStream);
        req.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        var res = await ClientHttpClient.SendAsync(req);

        if (!res.IsSuccessStatusCode) {
            Console.WriteLine($"Failed to upload file: {await res.Content.ReadAsStringAsync()}");
            throw new InvalidDataException($"Failed to upload file: {await res.Content.ReadAsStringAsync()}");
        }

        var resJson = await res.Content.ReadFromJsonAsync<JsonElement>();
        return resJson.GetProperty("content_uri").GetString()!;
    }

    public virtual async Task<GenericRoom> CreateRoom(CreateRoomRequest creationEvent, bool returnExistingIfAliasExists = false, bool joinIfAliasExists = false,
        bool inviteIfAliasExists = false) {
        if (returnExistingIfAliasExists) {
            var aliasRes = await ResolveRoomAliasAsync($"#{creationEvent.RoomAliasName}:{ServerName}");
            if (aliasRes?.RoomId != null) {
                var existingRoom = GetRoom(aliasRes.RoomId);
                if (joinIfAliasExists) await existingRoom.JoinAsync();

                if (inviteIfAliasExists) await existingRoom.InviteUsersAsync(creationEvent.Invite ?? new List<string>());

                return existingRoom;
            }
        }

        creationEvent.CreationContent["creator"] = WhoAmI.UserId;
        var res = await ClientHttpClient.PostAsJsonAsync("/_matrix/client/v3/createRoom", creationEvent, new JsonSerializerOptions {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
        if (!res.IsSuccessStatusCode) {
            Console.WriteLine($"Failed to create room: {await res.Content.ReadAsStringAsync()}");
            throw new InvalidDataException($"Failed to create room: {await res.Content.ReadAsStringAsync()}");
        }

        var room = GetRoom((await res.Content.ReadFromJsonAsync<JsonObject>())!["room_id"]!.ToString());

        if (creationEvent.Invite is not null)
            await room.InviteUsersAsync(creationEvent.Invite ?? new List<string>());

        return room;
    }

    public virtual async Task Logout() {
        var res = await ClientHttpClient.PostAsync("/_matrix/client/v3/logout", null);
        if (!res.IsSuccessStatusCode) {
            Console.WriteLine($"Failed to logout: {await res.Content.ReadAsStringAsync()}");
            throw new InvalidDataException($"Failed to logout: {await res.Content.ReadAsStringAsync()}");
        }
    }

#region Utility Functions

    public virtual async IAsyncEnumerable<GenericRoom> GetJoinedRoomsByType(string type, int? semaphoreCount = null) {
        var rooms = await GetJoinedRooms();
        SemaphoreSlim? semaphoreSlim = semaphoreCount is null ? null : new(semaphoreCount.Value, semaphoreCount.Value);
        var tasks = rooms.Select(async room => {
            while (true) {
                if (semaphoreSlim is not null) await semaphoreSlim.WaitAsync();
                try {
                    var roomType = await room.GetRoomType();
                    if (semaphoreSlim is not null) semaphoreSlim.Release();
                    if (roomType == type) return room;
                    return null;
                }
                catch (MatrixException e) {
                    throw;
                }
                catch (Exception e) {
                    Console.WriteLine($"Failed to get room type for {room.RoomId}: {e.Message}");
                    await Task.Delay(1000);
                }
            }
        }).ToAsyncResultEnumerable();

        await foreach (var result in tasks)
            if (result is not null)
                yield return result;
    }

#endregion

#region Account Data

    public virtual async Task<T> GetAccountDataAsync<T>(string key) =>
        // var res = await _httpClient.GetAsync($"/_matrix/client/v3/user/{UserId}/account_data/{key}");
        // if (!res.IsSuccessStatusCode) {
        //     Console.WriteLine($"Failed to get account data: {await res.Content.ReadAsStringAsync()}");
        //     throw new InvalidDataException($"Failed to get account data: {await res.Content.ReadAsStringAsync()}");
        // }
        //
        // return await res.Content.ReadFromJsonAsync<T>();
        await ClientHttpClient.GetFromJsonAsync<T>($"/_matrix/client/v3/user/{WhoAmI.UserId}/account_data/{key}");

    public virtual async Task<T?> GetAccountDataOrNullAsync<T>(string key) {
        try {
            return await GetAccountDataAsync<T>(key);
        }
        catch (MatrixException e) {
            if (e is { ErrorCode: MatrixException.ErrorCodes.M_NOT_FOUND }) return default;
            throw;
        }
    }

    public virtual async Task SetAccountDataAsync(string key, object data) {
        var res = await ClientHttpClient.PutAsJsonAsync($"/_matrix/client/v3/user/{WhoAmI.UserId}/account_data/{key}", data);
        if (!res.IsSuccessStatusCode) {
            Console.WriteLine($"Failed to set account data: {await res.Content.ReadAsStringAsync()}");
            throw new InvalidDataException($"Failed to set account data: {await res.Content.ReadAsStringAsync()}");
        }
    }

#endregion

#region MSC 4133

    public async Task UpdateProfilePropertyAsync(string name, object? value) {
        var caps = await GetCapabilitiesAsync();
        if (caps is null) throw new Exception("Failed to get capabilities");
    }

#endregion

    [Obsolete("This method assumes no support for MSC 4069 and MSC 4133")]
    public async Task UpdateProfileAsync(UserProfileResponse? newProfile, bool preserveCustomRoomProfile = true) {
        if (newProfile is null) return;
        Console.WriteLine($"Updating profile for {WhoAmI.UserId} to {newProfile.ToJson(ignoreNull: true)} (preserving room profiles: {preserveCustomRoomProfile})");
        var oldProfile = await GetProfileAsync(WhoAmI.UserId!);
        Dictionary<string, RoomMemberEventContent> expectedRoomProfiles = new();
        var syncHelper = new SyncHelper(this) {
            Filter = new SyncFilter {
                AccountData = new SyncFilter.EventFilter() {
                    Types = new List<string> {
                        "m.room.member"
                    }
                }
            },
            Timeout = 250
        };
        var targetSyncCount = 0;

        if (preserveCustomRoomProfile) {
            var rooms = await GetJoinedRooms();
            var roomProfiles = rooms.Select(GetOwnRoomProfileWithIdAsync).ToAsyncResultEnumerable();
            targetSyncCount = rooms.Count;
            await foreach (var (roomId, currentRoomProfile) in roomProfiles)
                try {
                    // var currentRoomProfile = await room.GetStateAsync<RoomMemberEventContent>("m.room.member", WhoAmI.UserId!);
                    //build new profiles
                    if (currentRoomProfile.DisplayName == oldProfile.DisplayName) currentRoomProfile.DisplayName = newProfile.DisplayName;

                    if (currentRoomProfile.AvatarUrl == oldProfile.AvatarUrl) currentRoomProfile.AvatarUrl = newProfile.AvatarUrl;

                    currentRoomProfile.Reason = null;

                    expectedRoomProfiles.Add(roomId, currentRoomProfile);
                }
                catch (Exception e) { }

            Console.WriteLine($"Rooms with custom profiles: {string.Join(',', expectedRoomProfiles.Keys)}");
        }

        if (oldProfile.DisplayName != newProfile.DisplayName)
            await ClientHttpClient.PutAsJsonAsync($"/_matrix/client/v3/profile/{WhoAmI.UserId}/displayname", new { displayname = newProfile.DisplayName });
        else
            Console.WriteLine($"Not updating display name because {oldProfile.DisplayName} == {newProfile.DisplayName}");

        if (oldProfile.AvatarUrl != newProfile.AvatarUrl)
            await ClientHttpClient.PutAsJsonAsync($"/_matrix/client/v3/profile/{WhoAmI.UserId}/avatar_url", new { avatar_url = newProfile.AvatarUrl });
        else
            Console.WriteLine($"Not updating avatar URL because {newProfile.AvatarUrl} == {newProfile.AvatarUrl}");

        if (!preserveCustomRoomProfile) return;

        var syncCount = 0;
        await foreach (var sync in syncHelper.EnumerateSyncAsync()) {
            if (sync.Rooms is null) break;
            List<Task> tasks = new();
            foreach (var (roomId, roomData) in sync.Rooms.Join)
                if (roomData.State is { Events.Count: > 0 }) {
                    var incommingRoomProfile =
                        roomData.State?.Events?.FirstOrDefault(x => x.Type == "m.room.member" && x.StateKey == WhoAmI.UserId)?.TypedContent as RoomMemberEventContent;
                    if (incommingRoomProfile is null) continue;
                    if (!expectedRoomProfiles.ContainsKey(roomId)) continue;
                    var targetRoomProfileOverride = expectedRoomProfiles[roomId];
                    var room = GetRoom(roomId);
                    if (incommingRoomProfile.DisplayName != targetRoomProfileOverride.DisplayName || incommingRoomProfile.AvatarUrl != targetRoomProfileOverride.AvatarUrl)
                        tasks.Add(room.SendStateEventAsync("m.room.member", WhoAmI.UserId, targetRoomProfileOverride));
                }

            await Task.WhenAll(tasks);
            await Task.Delay(1000);

            var differenceFound = false;
            if (syncCount++ >= targetSyncCount) {
                var profiles = GetRoomProfilesAsync();
                await foreach (var (roomId, profile) in profiles) {
                    if (!expectedRoomProfiles.ContainsKey(roomId)) {
                        Console.WriteLine($"Skipping profile check for {roomId} because its not in override list?");
                        continue;
                    }

                    var targetRoomProfileOverride = expectedRoomProfiles[roomId];
                    if (profile.DisplayName != targetRoomProfileOverride.DisplayName || profile.AvatarUrl != targetRoomProfileOverride.AvatarUrl) {
                        differenceFound = true;
                        break;
                    }
                }

                if (!differenceFound) return;
            }
        }
    }

    public async IAsyncEnumerable<KeyValuePair<string, RoomMemberEventContent>> GetRoomProfilesAsync() {
        var rooms = await GetJoinedRooms();
        var results = rooms.Select(GetOwnRoomProfileWithIdAsync).ToAsyncResultEnumerable();
        await foreach (var res in results) yield return res;
    }

    public async Task<RoomIdResponse> JoinRoomAsync(string roomId, List<string> homeservers = null, string? reason = null) {
        var joinUrl = $"/_matrix/client/v3/join/{HttpUtility.UrlEncode(roomId)}";
        Console.WriteLine($"Calling {joinUrl} with {homeservers?.Count ?? 0} via's...");
        if (homeservers is not { Count: > 0 }) {
            // Legacy room IDs: !abc:server.xyz
            if (roomId.Contains(':'))
                homeservers = [ServerName, roomId.Split(':')[1]];
            // v12+ room IDs: !<hash>
            else {
                homeservers = [ServerName];
                foreach (var room in await GetJoinedRooms()) {
                    homeservers.Add(await room.GetOriginHomeserverAsync());
                }
            }
        }

        var fullJoinUrl = $"{joinUrl}?server_name=" + string.Join("&server_name=", homeservers);
        var res = await ClientHttpClient.PostAsJsonAsync(fullJoinUrl, new {
            reason
        });
        return await res.Content.ReadFromJsonAsync<RoomIdResponse>() ?? throw new Exception("Failed to join room?");
    }

#region Room Profile Utility

    private async Task<KeyValuePair<string, RoomMemberEventContent>> GetOwnRoomProfileWithIdAsync(GenericRoom room) =>
        new(room.RoomId, await room.GetStateAsync<RoomMemberEventContent>("m.room.member", WhoAmI.UserId!));

#endregion

    public async Task SetImpersonate(string mxid) {
        if (ClientHttpClient.AdditionalQueryParameters.TryGetValue("user_id", out var existingMxid) && existingMxid == mxid && WhoAmI.UserId == mxid) return;
        ClientHttpClient.AdditionalQueryParameters["user_id"] = mxid;
        WhoAmI = await ClientHttpClient.GetFromJsonAsync<WhoAmIResponse>("/_matrix/client/v3/account/whoami");
    }

    /// <summary>
    ///   Upload a filter to the homeserver. Substitutes @me with the user's ID.
    /// </summary>
    /// <param name="filter"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public async Task<FilterIdResponse> UploadFilterAsync(SyncFilter filter) {
        List<List<string>?> senderLists = [
            filter.AccountData?.Senders,
            filter.AccountData?.NotSenders,
            filter.Presence?.Senders,
            filter.Presence?.NotSenders,
            filter.Room?.AccountData?.Senders,
            filter.Room?.AccountData?.NotSenders,
            filter.Room?.Ephemeral?.Senders,
            filter.Room?.Ephemeral?.NotSenders,
            filter.Room?.State?.Senders,
            filter.Room?.State?.NotSenders,
            filter.Room?.Timeline?.Senders,
            filter.Room?.Timeline?.NotSenders
        ];

        foreach (var list in senderLists)
            if (list is { Count: > 0 } && list.Contains("@me")) {
                list.Remove("@me");
                list.Add(UserId);
            }

        var resp = await ClientHttpClient.PostAsJsonAsync("/_matrix/client/v3/user/" + UserId + "/filter", filter);
        return await resp.Content.ReadFromJsonAsync<FilterIdResponse>() ?? throw new Exception("Failed to upload filter?");
    }

    public async Task<SyncFilter> GetFilterAsync(string filterId) {
        if (_filterCache.TryGetValue(filterId, out var filter)) return filter;
        var resp = await ClientHttpClient.GetAsync("/_matrix/client/v3/user/" + UserId + "/filter/" + filterId);
        return _filterCache[filterId] = await resp.Content.ReadFromJsonAsync<SyncFilter>() ?? throw new Exception("Failed to get filter?");
    }

    public class FilterIdResponse {
        [JsonPropertyName("filter_id")]
        public required string FilterId { get; set; }
    }

    /// <summary>
    ///   Enumerate all account data per room.
    ///   <b>Warning</b>: This uses /sync!
    /// </summary>
    /// <param name="includeGlobal">Include non-room account data</param>
    /// <returns>Dictionary of room IDs and their account data.</returns>
    /// <exception cref="Exception"></exception>
    public async Task<Dictionary<string, EventList?>> EnumerateAccountDataPerRoom(bool includeGlobal = false) {
        var syncHelper = new SyncHelper(this);
        syncHelper.FilterId = await NamedCaches.FilterCache.GetOrSetValueAsync(CommonSyncFilters.GetAccountDataWithRooms);
        var resp = await syncHelper.SyncAsync();
        if (resp is null) throw new Exception("Sync failed");
        var perRoomAccountData = new Dictionary<string, EventList?>();

        if (includeGlobal)
            perRoomAccountData[""] = resp.AccountData;
        foreach (var (roomId, room) in resp.Rooms?.Join ?? []) perRoomAccountData[roomId] = room.AccountData;

        return perRoomAccountData;
    }

    /// <summary>
    ///   Enumerate all non-room account data.
    ///   <b>Warning</b>: This uses /sync!
    /// </summary>
    /// <returns>All account data.</returns>
    /// <exception cref="Exception"></exception>
    public async Task<EventList?> EnumerateAccountData() {
        var syncHelper = new SyncHelper(this) {
            FilterId = await NamedCaches.FilterCache.GetOrSetValueAsync(CommonSyncFilters.GetAccountData)
        };
        var resp = await syncHelper.SyncAsync();
        return resp?.AccountData ?? throw new Exception("Sync failed");
    }

    private Dictionary<string, string>? _namedFilterCache;
    private Dictionary<string, SyncFilter> _filterCache = new();

    private static readonly SemaphoreCache<CapabilitiesResponse> CapabilitiesCache = new();

    public async Task<CapabilitiesResponse> GetCapabilitiesAsync() =>
        await CapabilitiesCache.GetOrAdd(ServerName, async () =>
            await ClientHttpClient.GetFromJsonAsync<CapabilitiesResponse>("/_matrix/client/v3/capabilities")
        );

    public class HsNamedCaches {
        internal HsNamedCaches(AuthenticatedHomeserverGeneric hs) {
            FileCache = new NamedFileCache(hs);
            FilterCache = new NamedFilterCache(hs);
        }

        public NamedFilterCache FilterCache { get; init; }
        public NamedFileCache FileCache { get; init; }
    }

#region Authenticated Media

    // TODO: implement /_matrix/client/v1/media/config when it's actually useful - https://spec.matrix.org/v1.11/client-server-api/#get_matrixclientv1mediaconfig
    private bool? _serverSupportsAuthMedia;

    public async Task<string> GetMediaUrlAsync(MxcUri mxcUri, string? filename = null, int? timeout = null) {
        if (_serverSupportsAuthMedia == true) return mxcUri.ToDownloadUri(BaseUrl, filename, timeout);
        if (_serverSupportsAuthMedia == false) return mxcUri.ToLegacyDownloadUri(BaseUrl, filename, timeout);

        try {
            // Console.WriteLine($"Trying authenticated media URL: {uri}");
            var res = await ClientHttpClient.SendAsync(new() {
                // Method = HttpMethod.Head, // This apparently doesn't work with Matrix-Media-Repo...
                Method = HttpMethod.Get,
                RequestUri = (new Uri(mxcUri.ToDownloadUri(BaseUrl, filename, timeout), string.IsNullOrWhiteSpace(BaseUrl) ? UriKind.Relative : UriKind.Absolute))
            });
            if (res.IsSuccessStatusCode) {
                _serverSupportsAuthMedia = true;
                return mxcUri.ToDownloadUri(BaseUrl, filename, timeout);
            }
        }
        catch (MatrixException e) {
            if (e is not { ErrorCode: "M_UNKNOWN" }) throw;
        }

        //fallback to legacy media
        try {
            // Console.WriteLine($"Trying legacy media URL: {uri}");
            var res = await ClientHttpClient.SendAsync(new() {
                // Method = HttpMethod.Head,
                Method = HttpMethod.Get,
                RequestUri = new(mxcUri.ToLegacyDownloadUri(BaseUrl, filename, timeout), string.IsNullOrWhiteSpace(BaseUrl) ? UriKind.Relative : UriKind.Absolute)
            });
            if (res.IsSuccessStatusCode) {
                _serverSupportsAuthMedia = false;
                return mxcUri.ToLegacyDownloadUri(BaseUrl, filename, timeout);
            }
        }
        catch (MatrixException e) {
            if (e is not { ErrorCode: "M_UNKNOWN" }) throw;
        }

        throw new LibMatrixException() {
            ErrorCode = LibMatrixException.ErrorCodes.M_UNSUPPORTED,
            Error = "Failed to get media URL"
        };
    }

    public async Task<Stream> GetMediaStreamAsync(string mxcUri, string? filename = null, int? timeout = null) {
        var uri = await GetMediaUrlAsync(mxcUri, filename, timeout);
        var res = await ClientHttpClient.GetAsync(uri);
        return await res.Content.ReadAsStreamAsync();
    }

    public async Task<Stream> GetThumbnailStreamAsync(MxcUri mxcUri, int width, int height, string? method = null, int? timeout = null) {
        var (serverName, mediaId) = mxcUri;

        try {
            var uri = new Uri($"/_matrix/client/v1/thumbnail/{serverName}/{mediaId}");
            uri = uri.AddQuery("width", width.ToString());
            uri = uri.AddQuery("height", height.ToString());
            if (!string.IsNullOrWhiteSpace(method)) uri = uri.AddQuery("method", method);
            if (timeout is not null) uri = uri.AddQuery("timeout_ms", timeout.ToString()!);

            var res = await ClientHttpClient.GetAsync(uri.ToString());
            return await res.Content.ReadAsStreamAsync();
        }
        catch (MatrixException e) {
            if (e is not { ErrorCode: "M_UNKNOWN" }) throw;
        }

        //fallback to legacy media
        try {
            var uri = new Uri($"/_matrix/media/v3/thumbnail/{serverName}/{mediaId}");
            uri = uri.AddQuery("width", width.ToString());
            uri = uri.AddQuery("height", height.ToString());
            if (!string.IsNullOrWhiteSpace(method)) uri = uri.AddQuery("method", method);
            if (timeout is not null) uri = uri.AddQuery("timeout_ms", timeout.ToString()!);

            var res = await ClientHttpClient.GetAsync(uri.ToString());
            return await res.Content.ReadAsStreamAsync();
        }
        catch (MatrixException e) {
            if (e is not { ErrorCode: "M_UNKNOWN" }) throw;
        }

        throw new LibMatrixException() {
            ErrorCode = LibMatrixException.ErrorCodes.M_UNSUPPORTED,
            Error = "Failed to download media"
        };
        // return default;
    }

    public async Task<Dictionary<string, JsonValue>?> GetUrlPreviewAsync(string url) {
        try {
            var res = await ClientHttpClient.GetAsync($"/_matrix/client/v1/media/preview_url?url={HttpUtility.UrlEncode(url)}");
            return await res.Content.ReadFromJsonAsync<Dictionary<string, JsonValue>>();
        }
        catch (MatrixException e) {
            if (e is not { ErrorCode: "M_UNRECOGNIZED" }) throw;
        }

        //fallback to legacy media
        try {
            var res = await ClientHttpClient.GetAsync($"/_matrix/media/v3/preview_url?url={HttpUtility.UrlEncode(url)}");
            return await res.Content.ReadFromJsonAsync<Dictionary<string, JsonValue>>();
        }
        catch (MatrixException e) {
            if (e is not { ErrorCode: "M_UNRECOGNIZED" }) throw;
        }

        throw new LibMatrixException() {
            ErrorCode = LibMatrixException.ErrorCodes.M_UNSUPPORTED,
            Error = "Failed to download URL preview"
        };
    }

#endregion

    public Task ReportRoomAsync(string roomId, string reason) =>
        ClientHttpClient.PostAsJsonAsync($"/_matrix/client/v3/rooms/{roomId}/report", new {
            reason
        });

    public async Task ReportRoomEventAsync(string roomId, string eventId, string reason, int score = 0, bool ignoreSender = false) {
        await ClientHttpClient.PostAsJsonAsync($"/_matrix/client/v3/rooms/{roomId}/report/{eventId}", new {
            reason,
            score
        });

        if (ignoreSender) {
            var eventContent = await GetRoom(roomId).GetEventAsync(eventId);
            var sender = eventContent.Sender;
            await IgnoreUserAsync(sender);
        }
    }

    public async Task ReportUserAsync(string userId, string reason, bool ignore = false) {
        await ClientHttpClient.PostAsJsonAsync($"/_matrix/client/v3/users/{userId}/report", new {
            reason
        });

        if (ignore) {
            await IgnoreUserAsync(userId);
        }
    }

    public async Task<IgnoredUserListEventContent> GetIgnoredUserListAsync() {
        return await GetAccountDataOrNullAsync<IgnoredUserListEventContent>(IgnoredUserListEventContent.EventId) ?? new();
    }

    public async Task IgnoreUserAsync(string userId, IgnoredUserListEventContent.IgnoredUserContent? content = null) {
        content ??= new();

        var ignoredUserList = await GetIgnoredUserListAsync();
        ignoredUserList.IgnoredUsers.TryAdd(userId, content);
        await SetAccountDataAsync(IgnoredUserListEventContent.EventId, ignoredUserList);
    }

    public class CapabilitiesResponse {
        [JsonPropertyName("capabilities")]
        public CapabilitiesContents Capabilities { get; set; }

        public class CapabilitiesContents {
            [JsonPropertyName("m.3pid_changes")]
            public BooleanCapability? ThreePidChanges { get; set; }

            [JsonPropertyName("m.change_password")]
            public BooleanCapability? ChangePassword { get; set; }

            [JsonPropertyName("m.get_login_token")]
            public BooleanCapability? GetLoginToken { get; set; }

            [JsonPropertyName("m.room_versions")]
            public RoomVersionsCapability? RoomVersions { get; set; }

            [JsonPropertyName("m.set_avatar_url")]
            public BooleanCapability? SetAvatarUrl { get; set; }

            [JsonPropertyName("m.set_displayname")]
            public BooleanCapability? SetDisplayName { get; set; }

            [JsonPropertyName("gay.rory.bulk_send_events")]
            public BooleanCapability? BulkSendEvents { get; set; }

            [JsonPropertyName("gay.rory.synapse_admin_extensions.room_list.query_events.v2")]
            public BooleanCapability? SynapseRoomListQueryEventsV2 { get; set; }

            [JsonExtensionData]
            public Dictionary<string, object>? AdditionalCapabilities { get; set; }
        }

        public class BooleanCapability {
            [JsonPropertyName("enabled")]
            public bool Enabled { get; set; }
        }

        public class RoomVersionsCapability {
            [JsonPropertyName("default")]
            public string? Default { get; set; }

            [JsonPropertyName("available")]
            public Dictionary<string, string>? Available { get; set; }
        }
    }

#region Room Directory/aliases

    public async Task SetRoomAliasAsync(string roomAlias, string roomId) {
        var resp = await ClientHttpClient.PutAsJsonAsync($"/_matrix/client/v3/directory/room/{HttpUtility.UrlEncode(roomAlias)}", new RoomIdResponse() {
            RoomId = roomId
        });
        if (!resp.IsSuccessStatusCode) {
            Console.WriteLine($"Failed to set room alias: {await resp.Content.ReadAsStringAsync()}");
            throw new InvalidDataException($"Failed to set room alias: {await resp.Content.ReadAsStringAsync()}");
        }
    }

    public async Task DeleteRoomAliasAsync(string roomAlias) {
        var resp = await ClientHttpClient.DeleteAsync("/_matrix/client/v3/directory/room/" + HttpUtility.UrlEncode(roomAlias));
        if (!resp.IsSuccessStatusCode) {
            Console.WriteLine($"Failed to set room alias: {await resp.Content.ReadAsStringAsync()}");
            throw new InvalidDataException($"Failed to set room alias: {await resp.Content.ReadAsStringAsync()}");
        }
    }

    public async Task<RoomAliasesResponse> GetLocalRoomAliasesAsync(string roomId) {
        var resp = await ClientHttpClient.GetAsync($"/_matrix/client/v3/rooms/{HttpUtility.UrlEncode(roomId)}/aliases");
        if (!resp.IsSuccessStatusCode) {
            Console.WriteLine($"Failed to get room aliases: {await resp.Content.ReadAsStringAsync()}");
            throw new InvalidDataException($"Failed to get room aliases: {await resp.Content.ReadAsStringAsync()}");
        }

        return await resp.Content.ReadFromJsonAsync<RoomAliasesResponse>() ?? throw new Exception("Failed to get room aliases?");
    }

    public class RoomAliasesResponse {
        [JsonPropertyName("aliases")]
        public required List<string> Aliases { get; set; }
    }

    public async Task<HttpResponseMessage> SetRoomDirectoryVisibilityAsync(string roomId, RoomDirectoryVisibilityResponse.VisibilityValue visibility)
        => await ClientHttpClient.PostAsJsonAsync($"/_matrix/client/v3/directory/list/room/{HttpUtility.UrlEncode(roomId)}", new RoomDirectoryVisibilityResponse {
            Visibility = visibility
        });

#endregion
}