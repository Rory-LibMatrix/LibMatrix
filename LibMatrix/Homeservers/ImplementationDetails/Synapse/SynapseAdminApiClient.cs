using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ArcaneLibs.Extensions;
using LibMatrix.Filters;
using LibMatrix.Homeservers.ImplementationDetails.Synapse.Models.Filters;
using LibMatrix.Homeservers.ImplementationDetails.Synapse.Models.Responses;
using LibMatrix.Responses;

namespace LibMatrix.Homeservers.ImplementationDetails.Synapse;

public class SynapseAdminApiClient(AuthenticatedHomeserverSynapse authenticatedHomeserver) {
    // https://github.com/element-hq/synapse/tree/develop/docs/admin_api
    // https://github.com/element-hq/synapse/tree/develop/docs/usage/administration/admin_api

#region Rooms

    public async IAsyncEnumerable<SynapseAdminRoomListResult.SynapseAdminRoomListResultRoom> SearchRoomsAsync(int limit = int.MaxValue, int chunkLimit = 250,
        string orderBy = "name", string dir = "f", string? searchTerm = null, SynapseAdminLocalRoomQueryFilter? localFilter = null) {
        SynapseAdminRoomListResult? res = null;
        var i = 0;
        int? totalRooms = null;
        do {
            var url = $"/_synapse/admin/v1/rooms?limit={Math.Min(limit, chunkLimit)}&dir={dir}&order_by={orderBy}";
            if (!string.IsNullOrEmpty(searchTerm)) url += $"&search_term={searchTerm}";

            if (res?.NextBatch is not null) url += $"&from={res.NextBatch}";

            Console.WriteLine($"--- ADMIN Querying Room List with URL: {url} - Already have {i} items... ---");

            res = await authenticatedHomeserver.ClientHttpClient.GetFromJsonAsync<SynapseAdminRoomListResult>(url);
            totalRooms ??= res.TotalRooms;
            Console.WriteLine(res.ToJson(false));
            foreach (var room in res.Rooms) {
                if (localFilter is not null) {
                    if (!room.RoomId.Contains(localFilter.RoomIdContains)) {
                        totalRooms--;
                        continue;
                    }

                    if (!room.Name?.Contains(localFilter.NameContains) == true) {
                        totalRooms--;
                        continue;
                    }

                    if (!room.CanonicalAlias?.Contains(localFilter.CanonicalAliasContains) == true) {
                        totalRooms--;
                        continue;
                    }

                    if (!room.Version.Contains(localFilter.VersionContains)) {
                        totalRooms--;
                        continue;
                    }

                    if (!room.Creator.Contains(localFilter.CreatorContains)) {
                        totalRooms--;
                        continue;
                    }

                    if (!room.Encryption?.Contains(localFilter.EncryptionContains) == true) {
                        totalRooms--;
                        continue;
                    }

                    if (!room.JoinRules?.Contains(localFilter.JoinRulesContains) == true) {
                        totalRooms--;
                        continue;
                    }

                    if (!room.GuestAccess?.Contains(localFilter.GuestAccessContains) == true) {
                        totalRooms--;
                        continue;
                    }

                    if (!room.HistoryVisibility?.Contains(localFilter.HistoryVisibilityContains) == true) {
                        totalRooms--;
                        continue;
                    }

                    if (localFilter.CheckFederation && room.Federatable != localFilter.Federatable) {
                        totalRooms--;
                        continue;
                    }

                    if (localFilter.CheckPublic && room.Public != localFilter.Public) {
                        totalRooms--;
                        continue;
                    }

                    if (room.JoinedMembers < localFilter.JoinedMembersGreaterThan || room.JoinedMembers > localFilter.JoinedMembersLessThan) {
                        totalRooms--;
                        continue;
                    }

                    if (room.JoinedLocalMembers < localFilter.JoinedLocalMembersGreaterThan || room.JoinedLocalMembers > localFilter.JoinedLocalMembersLessThan) {
                        totalRooms--;
                        continue;
                    }
                }
                // if (contentSearch is not null && !string.IsNullOrEmpty(contentSearch) &&
                //     !(
                //         room.Name?.Contains(contentSearch, StringComparison.InvariantCultureIgnoreCase) == true ||
                //         room.CanonicalAlias?.Contains(contentSearch, StringComparison.InvariantCultureIgnoreCase) == true ||
                //         room.Creator?.Contains(contentSearch, StringComparison.InvariantCultureIgnoreCase) == true
                //     )
                //    ) {
                //     totalRooms--;
                //     continue;
                // }

                i++;
                yield return room;
            }
        } while (i < Math.Min(limit, totalRooms ?? limit));
    }

#endregion

#region Users

    public async IAsyncEnumerable<SynapseAdminUserListResult.SynapseAdminUserListResultUser> SearchUsersAsync(int limit = int.MaxValue, int chunkLimit = 250,
        SynapseAdminLocalUserQueryFilter? localFilter = null) {
        // TODO: implement filters
        string? from = null;
        while (limit > 0) {
            var url = new Uri("/_synapse/admin/v3/users", UriKind.Relative);
            url = url.AddQuery("limit", Math.Min(limit, chunkLimit).ToString());
            if (!string.IsNullOrWhiteSpace(from)) url = url.AddQuery("from", from);
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
        var url = new Uri($"/_synapse/admin/v1/rooms/{roomId.UrlEncode()}/media", UriKind.Relative);
        return await authenticatedHomeserver.ClientHttpClient.GetFromJsonAsync<SynapseAdminRoomMediaListResult>(url.ToString());
    }

    // This is in the user admin API section
    // public async IAsyncEnumerable<SynapseAdminRoomMediaListResult>

#endregion
}