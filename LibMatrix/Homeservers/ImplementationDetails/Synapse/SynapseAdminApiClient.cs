using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ArcaneLibs.Extensions;
using LibMatrix.Filters;
using LibMatrix.Homeservers.ImplementationDetails.Synapse.Models.Filters;
using LibMatrix.Homeservers.ImplementationDetails.Synapse.Models.Responses;
using LibMatrix.Responses;

namespace LibMatrix.Homeservers.ImplementationDetails.Synapse;

public class SynapseAdminApiClient(AuthenticatedHomeserverSynapse authenticatedHomeserver) {
    // https://github.com/element-hq/synapse/tree/develop/docs/admin_api

#region Rooms

    public async IAsyncEnumerable<AdminRoomListResult.AdminRoomListResultRoom> SearchRoomsAsync(int limit = int.MaxValue, int chunkLimit = 250, string orderBy = "name",
        string dir = "f", string? searchTerm = null, SynapseAdminLocalRoomQueryFilter? localFilter = null) {
        AdminRoomListResult? res = null;
        var i = 0;
        int? totalRooms = null;
        do {
            var url = $"/_synapse/admin/v1/rooms?limit={Math.Min(limit, chunkLimit)}&dir={dir}&order_by={orderBy}";
            if (!string.IsNullOrEmpty(searchTerm)) url += $"&search_term={searchTerm}";

            if (res?.NextBatch is not null) url += $"&from={res.NextBatch}";

            Console.WriteLine($"--- ADMIN Querying Room List with URL: {url} - Already have {i} items... ---");

            res = await authenticatedHomeserver.ClientHttpClient.GetFromJsonAsync<AdminRoomListResult>(url);
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

    public async IAsyncEnumerable<AdminUserListResult.AdminUserListResultUser> SearchUsersAsync(int limit = int.MaxValue, int chunkLimit = 250,
        SynapseAdminLocalUserQueryFilter? localFilter = null) {
        // TODO: implement filters
        string? from = null;
        while (limit > 0) {
            var url = new Uri("/_synapse/admin/v3/users", UriKind.Relative);
            url = url.AddQuery("limit", Math.Min(limit, chunkLimit).ToString());
            if (!string.IsNullOrWhiteSpace(from)) url = url.AddQuery("from", from);
            Console.WriteLine($"--- ADMIN Querying User List with URL: {url} ---");
            // TODO: implement URI methods in http client
            var res = await authenticatedHomeserver.ClientHttpClient.GetFromJsonAsync<AdminUserListResult>(url.ToString());
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

#endregion
}