// #define LOG_SKIP

using System.CodeDom.Compiler;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArcaneLibs.Extensions;
using LibMatrix.EventTypes.Spec.State.RoomInfo;
using LibMatrix.Homeservers.ImplementationDetails.Synapse.Models.Filters;
using LibMatrix.Homeservers.ImplementationDetails.Synapse.Models.Requests;
using LibMatrix.Homeservers.ImplementationDetails.Synapse.Models.Responses;
using LibMatrix.Responses;
using LibMatrix.StructuredData;

namespace LibMatrix.Homeservers.ImplementationDetails.Synapse;

public class SynapseAdminApiClient(AuthenticatedHomeserverSynapse authenticatedHomeserver) {
    private SynapseAdminUserCleanupExecutor UserCleanupExecutor { get; } = new(authenticatedHomeserver);
    // https://github.com/element-hq/synapse/tree/develop/docs/admin_api
    // https://github.com/element-hq/synapse/tree/develop/docs/usage/administration/admin_api

#region Rooms

    public async IAsyncEnumerable<SynapseAdminRoomListResult.SynapseAdminRoomListResultRoom> SearchRoomsAsync(int limit = int.MaxValue, int chunkLimit = 250,
        string orderBy = "name", string dir = "f", string? searchTerm = null, SynapseAdminLocalRoomQueryFilter? localFilter = null,
        bool fetchTombstones = false, bool fetchTopics = false, bool fetchCreateEvents = false) {
        if (localFilter != null) {
            fetchTombstones |= localFilter.Tombstone.Enabled;
            fetchTopics |= localFilter.Topic.Enabled;
            fetchCreateEvents |= localFilter.RoomType.Enabled;
        }

        var serverCaps = await authenticatedHomeserver.GetCapabilitiesAsync();
        var serverSupportsQueryEventsV2 = serverCaps.Capabilities.SynapseRoomListQueryEventsV2?.Enabled ?? false;

        SynapseAdminRoomListResult? res = null;
        var i = 0;
        int? totalRooms = null;
        do {
            var url = $"/_synapse/admin/v1/rooms?limit={Math.Min(limit, chunkLimit)}&dir={dir}&order_by={orderBy}";

            if (!string.IsNullOrEmpty(searchTerm)) url += $"&search_term={searchTerm}";
            if (res?.NextBatch is not null) url += $"&from={res.NextBatch}";

            // nonstandard stuff
            if (fetchTombstones) url += "&gay.rory.synapse_admin_extensions.include_tombstone=true&emma_include_tombstone=true";
            if (fetchTopics) url += "&gay.rory.synapse_admin_extensions.include_topic=true";
            if (fetchCreateEvents) url += "&gay.rory.synapse_admin_extensions.include_create_event=true";

            Console.WriteLine($"--- ADMIN Querying Room List with URL: {url} - Already have {i} items... ---");

            res = await authenticatedHomeserver.ClientHttpClient.GetFromJsonAsync<SynapseAdminRoomListResult>(url);
            totalRooms ??= res.TotalRooms;
            // Console.WriteLine(res.ToJson(false));

            List<SynapseAdminRoomListResult.SynapseAdminRoomListResultRoom> keep = [];
            foreach (var room in res.Rooms) {
                if (localFilter is not null) {
                    if (!localFilter.RoomId.Matches(room.RoomId, StringComparison.OrdinalIgnoreCase)) {
                        totalRooms--;
#if LOG_SKIP
                        Console.WriteLine($"Skipped room {room.ToJson(indent: false)} on rule roomid.");
#endif
                        continue;
                    }

                    if (!localFilter.Name.Matches(room.Name ?? "", StringComparison.OrdinalIgnoreCase)) {
                        totalRooms--;
#if LOG_SKIP
                        Console.WriteLine($"Skipped room {room.ToJson(indent: false)} on rule roomname.");
#endif
                        continue;
                    }

                    if (!localFilter.CanonicalAlias.Matches(room.CanonicalAlias ?? "", StringComparison.OrdinalIgnoreCase)) {
                        totalRooms--;
#if LOG_SKIP
                        Console.WriteLine($"Skipped room {room.ToJson(indent: false)} on rule alias.");
#endif
                        continue;
                    }

                    if (!localFilter.Version.Matches(room.Version ?? "")) {
                        totalRooms--;
#if LOG_SKIP
                        Console.WriteLine($"Skipped room {room.ToJson(indent: false)} on rule version.");
#endif
                        continue;
                    }

                    if (!localFilter.Creator.Matches(room.Creator ?? "", StringComparison.OrdinalIgnoreCase)) {
                        totalRooms--;
#if LOG_SKIP
                        Console.WriteLine($"Skipped room {room.ToJson(indent: false)} on rule creator.");
#endif
                        continue;
                    }

                    if (!localFilter.Encryption.Matches(room.Encryption ?? "", StringComparison.OrdinalIgnoreCase)) {
                        totalRooms--;
#if LOG_SKIP
                        Console.WriteLine($"Skipped room {room.ToJson(indent: false)} on rule encryption.");
#endif
                        continue;
                    }

                    if (!localFilter.JoinRules.Matches(room.JoinRules ?? "", StringComparison.OrdinalIgnoreCase)) {
                        totalRooms--;
#if LOG_SKIP
                        Console.WriteLine($"Skipped room {room.ToJson(indent: false)} on rule joinrules.");
#endif
                        continue;
                    }

                    if (!localFilter.GuestAccess.Matches(room.GuestAccess ?? "", StringComparison.OrdinalIgnoreCase)) {
                        totalRooms--;
#if LOG_SKIP
                        Console.WriteLine($"Skipped room {room.ToJson(indent: false)} on rule guestaccess.");
#endif
                        continue;
                    }

                    if (!localFilter.HistoryVisibility.Matches(room.HistoryVisibility ?? "", StringComparison.OrdinalIgnoreCase)) {
                        totalRooms--;
#if LOG_SKIP
                        Console.WriteLine($"Skipped room {room.ToJson(indent: false)} on rule history visibility.");
#endif
                        continue;
                    }

                    if (!localFilter.Federation.Matches(room.Federatable)) {
                        totalRooms--;
#if LOG_SKIP
                        Console.WriteLine($"Skipped room {room.ToJson(indent: false)} on rule federation.");
#endif
                        continue;
                    }

                    if (!localFilter.Public.Matches(room.Public)) {
                        totalRooms--;
#if LOG_SKIP
                        Console.WriteLine($"Skipped room {room.ToJson(indent: false)} on rule public.");
#endif
                        continue;
                    }

                    if (!localFilter.StateEvents.Matches(room.StateEvents)) {
                        totalRooms--;
#if LOG_SKIP
                        Console.WriteLine($"Skipped room {room.ToJson(indent: false)} on rule state events.");
#endif
                        continue;
                    }

                    if (!localFilter.JoinedMembers.Matches(room.JoinedMembers)) {
                        totalRooms--;
#if LOG_SKIP
                        Console.WriteLine($"Skipped room {room.ToJson(indent: false)} on rule joined members: {localFilter.JoinedMembersGreaterThan} < {room.JoinedLocalMembers} < {localFilter.JoinedMembersLessThan}.");
#endif
                        continue;
                    }

                    if (!localFilter.JoinedLocalMembers.Matches(room.JoinedLocalMembers)) {
                        totalRooms--;
#if LOG_SKIP
                        Console.WriteLine($"Skipped room {room.ToJson(indent: false)} on rule joined local members: {localFilter.JoinedLocalMembersGreaterThan} < {room.JoinedLocalMembers} < {localFilter.JoinedLocalMembersLessThan}.");
#endif
                        continue;
                    }
                }

                i++;
                keep.Add(room);
            }

            var parallelisationLimit = new SemaphoreSlim(32, 32);
            List<Task<(SynapseAdminRoomListResult.SynapseAdminRoomListResultRoom room, MatrixEventResponse?[] tasks)>> tasks = [];

            async Task<(SynapseAdminRoomListResult.SynapseAdminRoomListResultRoom room, MatrixEventResponse?[] tasks)> fillTask(
                SynapseAdminRoomListResult.SynapseAdminRoomListResultRoom room) {
                if (serverSupportsQueryEventsV2) return (room, []);

                var fillTasks = await Task.WhenAll(((Task<MatrixEventResponse?>?[]) [
                        fetchTombstones && room.TombstoneEvent is null
                            ? parallelisationLimit.RunWithLockAsync(() => room.GetTombstoneEventAsync(authenticatedHomeserver))
                            : null!,
                        fetchTopics && room.TopicEvent is null
                            ? parallelisationLimit.RunWithLockAsync(() => room.GetTopicEventAsync(authenticatedHomeserver))
                            : null!,
                        fetchCreateEvents && room.CreateEvent is null
                            ? parallelisationLimit.RunWithLockAsync(() => room.GetCreateEventAsync(authenticatedHomeserver))
                            : null!,
                    ])
                    .Where(t => t != null)!
                );
                return (
                    room,
                    fillTasks
                );
            }

            tasks.AddRange(
                serverSupportsQueryEventsV2
                    ? keep.Select(x => Task.FromResult((x, (MatrixEventResponse?[])[])))
                    : keep.Select(fillTask)
            );

            // await Task.WhenAll(tasks);

            foreach (var taskRes in tasks) {
                var (room, _) = await taskRes;
                if (localFilter is not null) {
                    if (!localFilter.Tombstone.Matches(room.TombstoneEvent != null)) {
                        totalRooms--;
#if LOG_SKIP
                        Console.WriteLine($"Skipped room {room.ToJson(indent: false)} on rule tombstone.");
#endif
                        continue;
                    }

                    if (!localFilter.RoomType.Matches(room.CreateEvent?.ContentAs<RoomCreateEventContent>()?.Type)) {
                        totalRooms--;
#if LOG_SKIP
                        Console.WriteLine($"Skipped room {room.ToJson(indent: false)} on rule room type.");
#endif
                        continue;
                    }

                    if (!localFilter.Topic.Matches(room.TopicEvent?.ContentAs<RoomTopicEventContent>()?.Topic, StringComparison.OrdinalIgnoreCase)) {
                        totalRooms--;
#if LOG_SKIP
                        Console.WriteLine($"Skipped room {room.ToJson(indent: false)} on rule topic.");
#endif
                        continue;
                    }
                }

                yield return room;
            }
        } while (i < Math.Min(limit, totalRooms ?? limit));
    }

    public async Task<bool> CheckRoomKnownAsync(string roomId) {
        try {
            var createEvt = await GetRoomStateAsync(roomId, RoomCreateEventContent.EventId);
            if (createEvt.Events.FirstOrDefault(e => e.StateKey == "") is null)
                return false;
            var members = await GetRoomMembersAsync(roomId, localOnly: true);
            return members.Members.Count > 0;
        }
        catch (Exception e) {
            if (e is HttpRequestException { StatusCode: System.Net.HttpStatusCode.NotFound }) return false;
            if (e is MatrixException { ErrorCode: MatrixException.ErrorCodes.M_NOT_FOUND }) return false;
            throw;
        }
    }

#endregion

#region Users

    public async IAsyncEnumerable<SynapseAdminUserListResult.SynapseAdminUserListResultUser> SearchUsersAsync(int limit = int.MaxValue, int chunkLimit = 250,
        string orderBy = "name", string dir = "f",
        SynapseAdminLocalUserQueryFilter? localFilter = null) {
        // TODO: implement filters
        string? from = null;
        while (limit > 0) {
            var url = new Uri("/_synapse/admin/v3/users", UriKind.Relative);
            url = url.AddQuery("limit", Math.Min(limit, chunkLimit).ToString());
            if (!string.IsNullOrWhiteSpace(from)) url = url.AddQuery("from", from);
            if (!string.IsNullOrWhiteSpace(orderBy)) url = url.AddQuery("order_by", orderBy);
            if (!string.IsNullOrWhiteSpace(dir)) url = url.AddQuery("dir", dir);

            Console.WriteLine($"--- ADMIN Querying User List with URL: {url} ---");
            // TODO: implement URI methods in http client
            var res = await authenticatedHomeserver.ClientHttpClient.GetFromJsonAsync<SynapseAdminUserListResult>(url.ToString());
            foreach (var user in res.Users) {
                limit--;
                yield return user;
            }

            if (string.IsNullOrWhiteSpace(res.NextToken)) break;
            from = res.NextToken;
        }
    }

    public async Task<LoginResponse> LoginUserAsync(string userId, TimeSpan expireAfter) {
        var url = new Uri($"/_synapse/admin/v1/users/{userId.UrlEncode()}/login", UriKind.Relative);
        url.AddQuery("valid_until_ms", DateTimeOffset.UtcNow.Add(expireAfter).ToUnixTimeMilliseconds().ToString());
        var resp = await authenticatedHomeserver.ClientHttpClient.PostAsJsonAsync<JsonObject>(url.ToString(), new());
        var loginResp = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        loginResp.UserId = userId; // Synapse only returns the access token
        return loginResp;
    }

    public async Task<AuthenticatedHomeserverSynapse> GetHomeserverForUserAsync(string userId, TimeSpan expireAfter) {
        var loginResp = await LoginUserAsync(userId, expireAfter);
        var homeserver = new AuthenticatedHomeserverSynapse(
            authenticatedHomeserver.ServerName, authenticatedHomeserver.WellKnownUris, authenticatedHomeserver.Proxy, loginResp.AccessToken
        );
        await homeserver.Initialise();
        return homeserver;
    }

#endregion

#region Reports

    public async IAsyncEnumerable<SynapseAdminEventReportListResult.SynapseAdminEventReportListResultReport> GetEventReportsAsync(int limit = int.MaxValue, int chunkLimit = 250,
        string dir = "f", SynapseAdminLocalEventReportQueryFilter? filter = null) {
        // TODO: implement filters
        string? from = null;
        while (limit > 0) {
            var url = new Uri("/_synapse/admin/v1/event_reports", UriKind.Relative);
            url = url.AddQuery("limit", Math.Min(limit, chunkLimit).ToString());
            if (!string.IsNullOrWhiteSpace(from)) url = url.AddQuery("from", from);
            Console.WriteLine($"--- ADMIN Querying Reports with URL: {url} ---");
            var res = await authenticatedHomeserver.ClientHttpClient.GetFromJsonAsync<SynapseAdminEventReportListResult>(url.ToString());
            foreach (var report in res.Reports) {
                limit--;
                yield return report;
            }

            if (string.IsNullOrWhiteSpace(res.NextToken)) break;
            from = res.NextToken;
        }
    }

    public async Task<SynapseAdminEventReportListResult.SynapseAdminEventReportListResultReportWithDetails> GetEventReportDetailsAsync(string reportId) {
        var url = new Uri($"/_synapse/admin/v1/event_reports/{reportId.UrlEncode()}", UriKind.Relative);
        return await authenticatedHomeserver.ClientHttpClient
            .GetFromJsonAsync<SynapseAdminEventReportListResult.SynapseAdminEventReportListResultReportWithDetails>(url.ToString());
    }

    // Utility function to get details straight away
    public async IAsyncEnumerable<SynapseAdminEventReportListResult.SynapseAdminEventReportListResultReportWithDetails> GetEventReportsWithDetailsAsync(int limit = int.MaxValue,
        int chunkLimit = 250, string dir = "f", SynapseAdminLocalEventReportQueryFilter? filter = null) {
        Queue<Task<SynapseAdminEventReportListResult.SynapseAdminEventReportListResultReportWithDetails>> tasks = [];
        await foreach (var report in GetEventReportsAsync(limit, chunkLimit, dir, filter)) {
            tasks.Enqueue(GetEventReportDetailsAsync(report.Id));
            while (tasks.Peek().IsCompleted) yield return await tasks.Dequeue(); // early return if possible
        }

        while (tasks.Count > 0) yield return await tasks.Dequeue();
    }

    public async Task DeleteEventReportAsync(string reportId) {
        var url = new Uri($"/_synapse/admin/v1/event_reports/{reportId.UrlEncode()}", UriKind.Relative);
        await authenticatedHomeserver.ClientHttpClient.DeleteAsync(url.ToString());
    }

#endregion

#region Background Updates

    public async Task<bool> GetBackgroundUpdatesEnabledAsync() {
        var url = new Uri("/_synapse/admin/v1/background_updates/enabled", UriKind.Relative);
        // The return type is technically wrong, but includes the field we want.
        var resp = await authenticatedHomeserver.ClientHttpClient.GetFromJsonAsync<SynapseAdminBackgroundUpdateStatusResponse>(url.ToString());
        return resp.Enabled;
    }

    public async Task<bool> SetBackgroundUpdatesEnabledAsync(bool enabled) {
        var url = new Uri("/_synapse/admin/v1/background_updates/enabled", UriKind.Relative);
        // The used types are technically wrong, but include the field we want.
        var resp = await authenticatedHomeserver.ClientHttpClient.PostAsJsonAsync<JsonObject>(url.ToString(), new JsonObject {
            ["enabled"] = enabled
        });
        var json = await resp.Content.ReadFromJsonAsync<SynapseAdminBackgroundUpdateStatusResponse>();
        return json!.Enabled;
    }

    public async Task<SynapseAdminBackgroundUpdateStatusResponse> GetBackgroundUpdatesStatusAsync() {
        var url = new Uri("/_synapse/admin/v1/background_updates/status", UriKind.Relative);
        return await authenticatedHomeserver.ClientHttpClient.GetFromJsonAsync<SynapseAdminBackgroundUpdateStatusResponse>(url.ToString());
    }

    /// <summary>
    /// Run a background job
    /// </summary>
    /// <param name="jobName">One of "populate_stats_process_rooms" or "regenerate_directory"</param>
    public async Task RunBackgroundJobsAsync(string jobName) {
        var url = new Uri("/_synapse/admin/v1/background_updates/run", UriKind.Relative);
        await authenticatedHomeserver.ClientHttpClient.PostAsJsonAsync(url.ToString(), new JsonObject() {
            ["job_name"] = jobName
        });
    }

#endregion

#region Federation

    public async IAsyncEnumerable<SynapseAdminDestinationListResult.SynapseAdminDestinationListResultDestination> GetFederationDestinationsAsync(int limit = int.MaxValue,
        int chunkLimit = 250) {
        string? from = null;
        while (limit > 0) {
            var url = new Uri("/_synapse/admin/v1/federation/destinations", UriKind.Relative);
            url = url.AddQuery("limit", Math.Min(limit, chunkLimit).ToString());
            if (!string.IsNullOrWhiteSpace(from)) url = url.AddQuery("from", from);
            Console.WriteLine($"--- ADMIN Querying Federation Destinations with URL: {url} ---");
            var res = await authenticatedHomeserver.ClientHttpClient.GetFromJsonAsync<SynapseAdminDestinationListResult>(url.ToString());
            foreach (var dest in res.Destinations) {
                limit--;
                yield return dest;
            }
        }
    }

    public async Task<SynapseAdminDestinationListResult.SynapseAdminDestinationListResultDestination> GetFederationDestinationDetailsAsync(string destination) {
        var url = new Uri($"/_synapse/admin/v1/federation/destinations/{destination}", UriKind.Relative);
        return await authenticatedHomeserver.ClientHttpClient.GetFromJsonAsync<SynapseAdminDestinationListResult.SynapseAdminDestinationListResultDestination>(url.ToString());
    }

    public async IAsyncEnumerable<SynapseAdminDestinationRoomListResult.SynapseAdminDestinationRoomListResultRoom> GetFederationDestinationRoomsAsync(string destination,
        int limit = int.MaxValue, int chunkLimit = 250) {
        string? from = null;
        while (limit > 0) {
            var url = new Uri($"/_synapse/admin/v1/federation/destinations/{destination}/rooms", UriKind.Relative);
            url = url.AddQuery("limit", Math.Min(limit, chunkLimit).ToString());
            if (!string.IsNullOrWhiteSpace(from)) url = url.AddQuery("from", from);
            Console.WriteLine($"--- ADMIN Querying Federation Destination Rooms with URL: {url} ---");
            var res = await authenticatedHomeserver.ClientHttpClient.GetFromJsonAsync<SynapseAdminDestinationRoomListResult>(url.ToString());
            foreach (var room in res.Rooms) {
                limit--;
                yield return room;
            }
        }
    }

    public async Task ResetFederationConnectionTimeoutAsync(string destination) {
        await authenticatedHomeserver.ClientHttpClient.PostAsJsonAsync($"/_synapse/admin/v1/federation/destinations/{destination}/reset_connection", new JsonObject());
    }

#endregion

#region Registration Tokens

    // does not support pagination
    public async Task<List<SynapseAdminRegistrationTokenListResult.SynapseAdminRegistrationTokenListResultToken>> GetRegistrationTokensAsync() {
        var url = new Uri("/_synapse/admin/v1/registration_tokens", UriKind.Relative);
        var resp = await authenticatedHomeserver.ClientHttpClient.GetFromJsonAsync<SynapseAdminRegistrationTokenListResult>(url.ToString());
        return resp.RegistrationTokens;
    }

    public async Task<SynapseAdminRegistrationTokenListResult.SynapseAdminRegistrationTokenListResultToken> GetRegistrationTokenAsync(string token) {
        var url = new Uri($"/_synapse/admin/v1/registration_tokens/{token.UrlEncode()}", UriKind.Relative);
        var resp =
            await authenticatedHomeserver.ClientHttpClient.GetFromJsonAsync<SynapseAdminRegistrationTokenListResult.SynapseAdminRegistrationTokenListResultToken>(url.ToString());
        return resp;
    }

    public async Task<SynapseAdminRegistrationTokenListResult.SynapseAdminRegistrationTokenListResultToken> CreateRegistrationTokenAsync(
        SynapseAdminRegistrationTokenCreateRequest request) {
        var url = new Uri("/_synapse/admin/v1/", UriKind.Relative);
        var resp = await authenticatedHomeserver.ClientHttpClient.PostAsJsonAsync(url.ToString(), request);
        var token = await resp.Content.ReadFromJsonAsync<SynapseAdminRegistrationTokenListResult.SynapseAdminRegistrationTokenListResultToken>();
        return token!;
    }

    public async Task<SynapseAdminRegistrationTokenListResult.SynapseAdminRegistrationTokenListResultToken> UpdateRegistrationTokenAsync(string token,
        SynapseAdminRegistrationTokenUpdateRequest request) {
        var url = new Uri($"/_synapse/admin/v1/registration_tokens/{token.UrlEncode()}", UriKind.Relative);
        var resp = await authenticatedHomeserver.ClientHttpClient.PutAsJsonAsync(url.ToString(), request);
        return await resp.Content.ReadFromJsonAsync<SynapseAdminRegistrationTokenListResult.SynapseAdminRegistrationTokenListResultToken>();
    }

    public async Task DeleteRegistrationTokenAsync(string token) {
        var url = new Uri($"/_synapse/admin/v1/registration_tokens/{token.UrlEncode()}", UriKind.Relative);
        await authenticatedHomeserver.ClientHttpClient.DeleteAsync(url.ToString());
    }

#endregion

#region Account Validity

    // Does anyone even use this?
    // README: https://github.com/matrix-org/synapse/issues/15271
    // -> Don't implement unless requested, if not for this feature almost never being used.

#endregion

#region Experimental Features

    public async Task<Dictionary<string, bool>> GetExperimentalFeaturesAsync(string userId) {
        var url = new Uri($"/_synapse/admin/v1/experimental_features/{userId.UrlEncode()}", UriKind.Relative);
        var resp = await authenticatedHomeserver.ClientHttpClient.GetFromJsonAsync<JsonObject>(url.ToString());
        return resp["features"]!.GetValue<Dictionary<string, bool>>();
    }

    public async Task SetExperimentalFeaturesAsync(string userId, Dictionary<string, bool> features) {
        var url = new Uri($"/_synapse/admin/v1/experimental_features/{userId.UrlEncode()}", UriKind.Relative);
        await authenticatedHomeserver.ClientHttpClient.PostAsJsonAsync<JsonObject>(url.ToString(), new JsonObject {
            ["features"] = JsonSerializer.Deserialize<JsonObject>(features.ToJson())
        });
    }

#endregion

#region Media

    public async Task<SynapseAdminRoomMediaListResult> GetRoomMediaAsync(string roomId) {
        var url = new Uri($"/_synapse/admin/v1/room/{roomId.UrlEncode()}/media", UriKind.Relative);
        return await authenticatedHomeserver.ClientHttpClient.GetFromJsonAsync<SynapseAdminRoomMediaListResult>(url.ToString());
    }

    // This is in the user admin API section
    // public async IAsyncEnumerable<SynapseAdminRoomMediaListResult>

#endregion

    public async Task<SynapseAdminUserRedactIdResponse?> DeleteAllMessages(string mxid, List<string>? rooms = null, string? reason = null, int? limit = 100000,
        bool waitForCompletion = true) {
        rooms ??= [];

        Dictionary<string, object> payload = new();
        if (rooms.Count > 0) payload["rooms"] = rooms;
        if (!string.IsNullOrEmpty(reason)) payload["reason"] = reason;
        if (limit.HasValue) payload["limit"] = limit.Value;

        var redactIdResp = await authenticatedHomeserver.ClientHttpClient.PostAsJsonAsync($"/_synapse/admin/v1/user/{mxid}/redact", payload);
        var redactId = await redactIdResp.Content.ReadFromJsonAsync<SynapseAdminUserRedactIdResponse>();

        if (waitForCompletion) {
            while (true) {
                var status = await GetRedactStatus(redactId!.RedactionId);
                if (status?.Status != "pending") break;
                await Task.Delay(1000);
            }
        }

        return redactId;
    }

    public async Task<SynapseAdminRedactStatusResponse?> GetRedactStatus(string redactId) {
        return await authenticatedHomeserver.ClientHttpClient.GetFromJsonAsync<SynapseAdminRedactStatusResponse>(
            $"/_synapse/admin/v1/user/redact_status/{redactId}");
    }

    public async Task DeactivateUserAsync(string mxid, bool erase = false, bool eraseMessages = false, bool extraCleanup = false) {
        if (eraseMessages) {
            await DeleteAllMessages(mxid);
        }

        if (extraCleanup) {
            await UserCleanupExecutor.CleanupUser(mxid);
        }

        await authenticatedHomeserver.ClientHttpClient.PostAsJsonAsync($"/_synapse/admin/v1/deactivate", new { erase });
    }

    public async Task ResetPasswordAsync(string mxid, string newPassword, bool logoutDevices = false) {
        await authenticatedHomeserver.ClientHttpClient.PostAsJsonAsync($"/_synapse/admin/v1/reset_password/{mxid}",
            new { new_password = newPassword, logout_devices = logoutDevices });
    }

    public async Task<SynapseAdminUserMediaResult> GetUserMediaAsync(string mxid, int? limit = 100, string? from = null, string? orderBy = null, string? dir = null) {
        var url = $"/_synapse/admin/v1/users/{mxid}/media";
        if (limit.HasValue) url += $"?limit={limit}";
        if (!string.IsNullOrEmpty(from)) url += $"&from={from}";
        if (!string.IsNullOrEmpty(orderBy)) url += $"&order_by={orderBy}";
        if (!string.IsNullOrEmpty(dir)) url += $"&dir={dir}";
        return await authenticatedHomeserver.ClientHttpClient.GetFromJsonAsync<SynapseAdminUserMediaResult>(url);
    }

    public async IAsyncEnumerable<SynapseAdminUserMediaResult.MediaInfo> GetUserMediaEnumerableAsync(string mxid, int chunkSize = 100, string? orderBy = null, string? dir = null) {
        SynapseAdminUserMediaResult? res = null;
        do {
            res = await GetUserMediaAsync(mxid, chunkSize, res?.NextToken, orderBy, dir);
            foreach (var media in res.Media) {
                yield return media;
            }
        } while (!string.IsNullOrEmpty(res.NextToken));
    }

    public async Task BlockRoom(string roomId, bool block = true) {
        await authenticatedHomeserver.ClientHttpClient.PutAsJsonAsync($"/_synapse/admin/v1/rooms/{roomId.UrlEncode()}/block", new {
            block
        });
    }

    public async Task<SynapseAdminRoomDeleteResponse> DeleteRoom(string roomId, SynapseAdminRoomDeleteRequest request, bool waitForCompletion = true) {
        var resp = await authenticatedHomeserver.ClientHttpClient.DeleteAsJsonAsync($"/_synapse/admin/v2/rooms/{roomId.UrlEncode()}", request);
        var deleteResp = await resp.Content.ReadFromJsonAsync<SynapseAdminRoomDeleteResponse>();

        if (waitForCompletion) {
            while (true) {
                var status = await GetRoomDeleteStatus(deleteResp!.DeleteId);
                if (status?.Status != "pending") break;
                await Task.Delay(1000);
            }
        }

        return deleteResp!;
    }

    public async Task<SynapseAdminRoomDeleteStatus> GetRoomDeleteStatusByRoomId(string roomId) {
        return await authenticatedHomeserver.ClientHttpClient.GetFromJsonAsync<SynapseAdminRoomDeleteStatus>(
            $"/_synapse/admin/v2/rooms/{roomId.UrlEncode()}/delete_status");
    }

    public async Task<SynapseAdminRoomDeleteStatus> GetRoomDeleteStatus(string deleteId) {
        return await authenticatedHomeserver.ClientHttpClient.GetFromJsonAsync<SynapseAdminRoomDeleteStatus>(
            $"/_synapse/admin/v2/rooms/delete_status/{deleteId}");
    }

    public async Task<SynapseAdminRoomMemberListResult> GetRoomMembersAsync(string roomId, bool localOnly = false) {
        var res = await authenticatedHomeserver.ClientHttpClient.GetFromJsonAsync<SynapseAdminRoomMemberListResult>($"/_synapse/admin/v1/rooms/{roomId.UrlEncode()}/members");
        if (localOnly) {
            res.Members = res.Members.Where(m => m.EndsWith($":{authenticatedHomeserver.ServerName}")).ToList();
        }

        return res;
    }

    public async Task<SynapseAdminRoomStateResult> GetRoomStateAsync(string roomId, string? type = null) {
        return string.IsNullOrWhiteSpace(type)
            ? await authenticatedHomeserver.ClientHttpClient.GetFromJsonAsync<SynapseAdminRoomStateResult>($"/_synapse/admin/v1/rooms/{roomId.UrlEncode()}/state")
            : await authenticatedHomeserver.ClientHttpClient.GetFromJsonAsync<SynapseAdminRoomStateResult>($"/_synapse/admin/v1/rooms/{roomId.UrlEncode()}/state?type={type}");
    }

    public async Task QuarantineMediaByRoomId(string roomId) {
        await authenticatedHomeserver.ClientHttpClient.PutAsJsonAsync($"/_synapse/admin/v1/room/{roomId.UrlEncode()}/media/quarantine", new { });
    }

    public async Task QuarantineMediaByUserId(string mxid) {
        await authenticatedHomeserver.ClientHttpClient.PutAsJsonAsync($"/_synapse/admin/v1/user/{mxid}/media/quarantine", new { });
    }

    public async Task QuarantineMediaById(string serverName, string mediaId) {
        await authenticatedHomeserver.ClientHttpClient.PutAsJsonAsync($"/_synapse/admin/v1/media/quarantine/{serverName}/{mediaId}", new { });
    }

    public async Task QuarantineMediaById(MxcUri mxcUri) {
        await authenticatedHomeserver.ClientHttpClient.PutAsJsonAsync($"/_synapse/admin/v1/media/quarantine/{mxcUri.ServerName}/{mxcUri.MediaId}", new { });
    }

    public async Task DeleteMediaById(string serverName, string mediaId) {
        await authenticatedHomeserver.ClientHttpClient.DeleteAsync($"/_synapse/admin/v1/media/{serverName}/{mediaId}");
    }

    public async Task DeleteMediaById(MxcUri mxcUri) {
        await authenticatedHomeserver.ClientHttpClient.DeleteAsync($"/_synapse/admin/v1/media/{mxcUri.ServerName}/{mxcUri.MediaId}");
    }
}