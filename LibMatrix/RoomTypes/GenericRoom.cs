using System.Collections.Frozen;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Web;
using ArcaneLibs.Extensions;
using LibMatrix.EventTypes;
using LibMatrix.EventTypes.Spec;
using LibMatrix.EventTypes.Spec.State;
using LibMatrix.EventTypes.Spec.State.RoomInfo;
using LibMatrix.Filters;
using LibMatrix.Helpers;
using LibMatrix.Homeservers;
using LibMatrix.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace LibMatrix.RoomTypes;

public class GenericRoom {
    public readonly AuthenticatedHomeserverGeneric Homeserver;

    public GenericRoom(AuthenticatedHomeserverGeneric homeserver, string roomId) {
        if (string.IsNullOrWhiteSpace(roomId))
            throw new ArgumentException("Room ID cannot be null or whitespace", nameof(roomId));
        Homeserver = homeserver;
        RoomId = roomId;
        // if (GetType() != typeof(SpaceRoom))
        if (GetType() == typeof(GenericRoom)) AsSpace = new SpaceRoom(homeserver, RoomId);
    }

    public string RoomId { get; set; }

    public async IAsyncEnumerable<StateEventResponse?> GetFullStateAsync() {
        var result = Homeserver.ClientHttpClient.GetAsyncEnumerableFromJsonAsync<StateEventResponse>($"/_matrix/client/v3/rooms/{RoomId}/state");
        await foreach (var resp in result) yield return resp;
    }

    public Task<List<StateEventResponse>> GetFullStateAsListAsync() =>
        Homeserver.ClientHttpClient.GetFromJsonAsync<List<StateEventResponse>>($"/_matrix/client/v3/rooms/{RoomId}/state");

    public async Task<T?> GetStateAsync<T>(string type, string stateKey = "") {
        if (string.IsNullOrEmpty(type)) throw new ArgumentNullException(nameof(type), "Event type must be specified");
        var url = $"/_matrix/client/v3/rooms/{RoomId}/state/{type}";
        if (!string.IsNullOrEmpty(stateKey)) url += $"/{stateKey}";
        try {
            var resp = await Homeserver.ClientHttpClient.GetFromJsonAsync<T>(url);
            return resp;
        }
        catch (MatrixException e) {
            // if (e is not { ErrorCodode: "M_NOT_FOUND" }) {
            throw;
            // }

            // Console.WriteLine(e);
            // return default;
        }
    }

    public async Task<T?> GetStateOrNullAsync<T>(string type, string stateKey = "") {
        try {
            return await GetStateAsync<T>(type, stateKey);
        }
        catch (MatrixException e) {
            if (e.ErrorCode == "M_NOT_FOUND") return default;
            throw;
        }
    }

    public async Task<StateEventResponse> GetStateEventAsync(string type, string stateKey = "") {
        if (string.IsNullOrEmpty(type)) throw new ArgumentNullException(nameof(type), "Event type must be specified");
        var url = $"/_matrix/client/v3/rooms/{RoomId}/state/{type}";
        if (!string.IsNullOrEmpty(stateKey)) url += $"/{stateKey}";
        url += "?format=event";
        try {
            var resp = await Homeserver.ClientHttpClient.GetFromJsonAsync<JsonObject>(url);
            if (resp["type"]?.GetValue<string>() != type)
                throw new LibMatrixException() {
                    Error = "Homeserver returned event type does not match requested type, or server does not support passing `format`.",
                    ErrorCode = LibMatrixException.ErrorCodes.M_UNSUPPORTED
                };
            // throw new InvalidDataException("Returned event type does not match requested type, or server does not support passing `format`.");
            return resp.Deserialize<StateEventResponse>();
        }
        catch (MatrixException e) {
            // if (e is not { ErrorCodode: "M_NOT_FOUND" }) {
            throw;
            // }

            // Console.WriteLine(e);
            // return default;
        }
    }

    public async Task<string?> GetStateEventIdAsync(string type, string stateKey = "", bool fallbackToSync = true) {
        try {
            return (await GetStateEventAsync(type, stateKey)).EventId ?? throw new LibMatrixException() {
                ErrorCode = LibMatrixException.ErrorCodes.M_UNSUPPORTED,
                Error = "Homeserver does not include event ID in state events."
            };
        }
        catch (LibMatrixException e) {
            if (e.ErrorCode == LibMatrixException.ErrorCodes.M_UNSUPPORTED) {
                if (!fallbackToSync) throw;
                Console.WriteLine("WARNING: Homeserver does not support getting event ID from state events, falling back to sync");
                var sh = new SyncHelper(Homeserver);
                var emptyFilter = new SyncFilter.EventFilter(types: [], limit: 1, senders: [], notTypes: ["*"]);
                var emptyStateFilter = new SyncFilter.RoomFilter.StateFilter(types: [], limit: 1, senders: [], notTypes: ["*"], rooms: []);
                sh.Filter = new() {
                    Presence = emptyFilter,
                    AccountData = emptyFilter,
                    Room = new SyncFilter.RoomFilter() {
                        AccountData = emptyStateFilter,
                        Timeline = emptyStateFilter,
                        Ephemeral = emptyStateFilter,
                        State = new SyncFilter.RoomFilter.StateFilter(),
                        Rooms = [RoomId]
                    }
                };
                var sync = await sh.SyncAsync();
                var state = sync.Rooms.Join[RoomId].State.Events;
                var stateEvent = state.FirstOrDefault(x => x.Type == type && x.StateKey == stateKey);
                if (stateEvent is null)
                    throw new LibMatrixException() {
                        ErrorCode = LibMatrixException.ErrorCodes.M_NOT_FOUND,
                        Error = "State event not found in sync response"
                    };
                return stateEvent.EventId;
            }

            return null;
        }
    }

    public async Task<StateEventResponse?> GetStateEventOrNullAsync(string type, string stateKey = "") {
        try {
            return await GetStateEventAsync(type, stateKey);
        }
        catch (MatrixException e) {
            if (e.ErrorCode == "M_NOT_FOUND") return default;
            throw;
        }
    }

    public async Task<MessagesResponse> GetMessagesAsync(string from = "", int? limit = null, string dir = "b", string filter = "") {
        var url = $"/_matrix/client/v3/rooms/{RoomId}/messages?dir={dir}";
        if (!string.IsNullOrWhiteSpace(from)) url += $"&from={from}";
        if (limit is not null) url += $"&limit={limit}";
        if (!string.IsNullOrWhiteSpace(filter)) url += $"&filter={filter}";
        var res = await Homeserver.ClientHttpClient.GetFromJsonAsync<MessagesResponse>(url);
        return res;
    }

    /// <summary>
    /// Same as <see cref="GetMessagesAsync"/>, except keeps fetching more responses until the beginning of the room is found, or the target message limit is reached
    /// </summary>
    public async IAsyncEnumerable<MessagesResponse> GetManyMessagesAsync(string from = "", int limit = 100, string dir = "b", string filter = "", bool includeState = true,
        bool fixForward = false, int chunkSize = 100) {
        if (dir == "f" && fixForward) {
            var concat = new List<MessagesResponse>();
            while (true) {
                var resp = await GetMessagesAsync(from, int.MaxValue, "b", filter);
                concat.Add(resp);
                if (!includeState)
                    resp.State.Clear();
                if (resp.End is null) break;
                from = resp.End;
            }

            concat.Reverse();
            foreach (var eventResponse in concat) {
                limit -= eventResponse.State.Count + eventResponse.Chunk.Count;
                while (limit < 0) {
                    if (eventResponse.State.Count > 0 && eventResponse.State.Max(x => x.OriginServerTs) > eventResponse.Chunk.Max(x => x.OriginServerTs))
                        eventResponse.State.Remove(eventResponse.State.MaxBy(x => x.OriginServerTs));
                    else
                        eventResponse.Chunk.Remove(eventResponse.Chunk.MaxBy(x => x.OriginServerTs));

                    limit++;
                }

                eventResponse.Chunk.Reverse();
                eventResponse.State.Reverse();
                yield return eventResponse;
                if (limit <= 0) yield break;
            }
        }
        else
            while (limit > 0) {
                var resp = await GetMessagesAsync(from, Math.Min(chunkSize, limit), dir, filter);

                if (!includeState)
                    resp.State.Clear();

                limit -= resp.Chunk.Count + resp.State.Count;
                yield return resp;
                if (resp.End is null) {
                    Console.WriteLine("End is null");
                    yield break;
                }

                from = resp.End;
            }

        Console.WriteLine("End of GetManyAsync");
    }

    public async Task<string?> GetNameAsync() => (await GetStateOrNullAsync<RoomNameEventContent>("m.room.name"))?.Name;

    public async Task<RoomIdResponse> JoinAsync(string[]? homeservers = null, string? reason = null, bool checkIfAlreadyMember = true) {
        if (checkIfAlreadyMember)
            try {
                var ser = await GetStateEventOrNullAsync(RoomMemberEventContent.EventId, Homeserver.UserId);
                if (ser?.TypedContent is RoomMemberEventContent { Membership: "join" })
                    return new RoomIdResponse {
                        RoomId = RoomId
                    };
            }
            catch { } //ignore

        var joinUrl = $"/_matrix/client/v3/join/{HttpUtility.UrlEncode(RoomId)}";
        Console.WriteLine($"Calling {joinUrl} with {homeservers?.Length ?? 0} via's...");
        if (homeservers == null || homeservers.Length == 0) homeservers = new[] { RoomId.Split(':', 2)[1] };
        var fullJoinUrl = $"{joinUrl}?server_name=" + string.Join("&server_name=", homeservers);
        var res = await Homeserver.ClientHttpClient.PostAsJsonAsync(fullJoinUrl, new {
            reason
        });
        return await res.Content.ReadFromJsonAsync<RoomIdResponse>() ?? throw new Exception("Failed to join room?");
    }

    public async IAsyncEnumerable<StateEventResponse> GetMembersEnumerableAsync(bool joinedOnly = true) {
        // var sw = Stopwatch.StartNew();
        var res = await Homeserver.ClientHttpClient.GetAsync($"/_matrix/client/v3/rooms/{RoomId}/members");
        // if (sw.ElapsedMilliseconds > 1000)
        // Console.WriteLine($"Members call responded in {sw.GetElapsedAndRestart()}");
        // else sw.Restart();
        // var resText = await res.Content.ReadAsStringAsync();
        // Console.WriteLine($"Members call response read in {sw.GetElapsedAndRestart()}");
        var result = await JsonSerializer.DeserializeAsync<ChunkedStateEventResponse>(await res.Content.ReadAsStreamAsync(), new JsonSerializerOptions() {
            TypeInfoResolver = ChunkedStateEventResponseSerializerContext.Default
        });
        // if (sw.ElapsedMilliseconds > 100)
        // Console.WriteLine($"Members call deserialised in {sw.GetElapsedAndRestart()}");
        // else sw.Restart();
        foreach (var resp in result.Chunk) {
            if (resp?.Type != "m.room.member") continue;
            if (joinedOnly && resp.RawContent?["membership"]?.GetValue<string>() != "join") continue;
            yield return resp;
        }

        // if (sw.ElapsedMilliseconds > 100)
        // Console.WriteLine($"Members call iterated in {sw.GetElapsedAndRestart()}");
    }

    public async Task<FrozenSet<StateEventResponse>> GetMembersListAsync(bool joinedOnly = true) {
        // var sw = Stopwatch.StartNew();
        var res = await Homeserver.ClientHttpClient.GetAsync($"/_matrix/client/v3/rooms/{RoomId}/members");
        // if (sw.ElapsedMilliseconds > 1000)
        // Console.WriteLine($"Members call responded in {sw.GetElapsedAndRestart()}");
        // else sw.Restart();
        // var resText = await res.Content.ReadAsStringAsync();
        // Console.WriteLine($"Members call response read in {sw.GetElapsedAndRestart()}");
        var result = await JsonSerializer.DeserializeAsync<ChunkedStateEventResponse>(await res.Content.ReadAsStreamAsync(), new JsonSerializerOptions() {
            TypeInfoResolver = ChunkedStateEventResponseSerializerContext.Default
        });
        // if (sw.ElapsedMilliseconds > 100)
        // Console.WriteLine($"Members call deserialised in {sw.GetElapsedAndRestart()}");
        // else sw.Restart();
        var members = new List<StateEventResponse>();
        foreach (var resp in result.Chunk) {
            if (resp?.Type != "m.room.member") continue;
            if (joinedOnly && resp.RawContent?["membership"]?.GetValue<string>() != "join") continue;
            members.Add(resp);
        }

        // if (sw.ElapsedMilliseconds > 100)
        // Console.WriteLine($"Members call iterated in {sw.GetElapsedAndRestart()}");
        return members.ToFrozenSet();
    }

#region Utility shortcuts

    public Task<EventIdResponse> SendMessageEventAsync(RoomMessageEventContent content) =>
        SendTimelineEventAsync("m.room.message", content);

    public async Task<List<string>?> GetAliasesAsync() {
        var res = await GetStateAsync<RoomAliasEventContent>("m.room.aliases");
        return res.Aliases;
    }

    public Task<RoomCanonicalAliasEventContent?> GetCanonicalAliasAsync() =>
        GetStateAsync<RoomCanonicalAliasEventContent>("m.room.canonical_alias");

    public Task<RoomTopicEventContent?> GetTopicAsync() =>
        GetStateAsync<RoomTopicEventContent>("m.room.topic");

    public Task<RoomAvatarEventContent?> GetAvatarUrlAsync() =>
        GetStateAsync<RoomAvatarEventContent>("m.room.avatar");

    public Task<RoomJoinRulesEventContent?> GetJoinRuleAsync() =>
        GetStateAsync<RoomJoinRulesEventContent>("m.room.join_rules");

    public Task<RoomHistoryVisibilityEventContent?> GetHistoryVisibilityAsync() =>
        GetStateAsync<RoomHistoryVisibilityEventContent?>("m.room.history_visibility");

    public Task<RoomGuestAccessEventContent?> GetGuestAccessAsync() =>
        GetStateAsync<RoomGuestAccessEventContent>("m.room.guest_access");

    public Task<RoomCreateEventContent?> GetCreateEventAsync() =>
        GetStateAsync<RoomCreateEventContent>("m.room.create");

    public async Task<string?> GetRoomType() {
        var res = await GetStateAsync<RoomCreateEventContent>("m.room.create");
        return res.Type;
    }

    public Task<RoomPowerLevelEventContent?> GetPowerLevelsAsync() =>
        GetStateAsync<RoomPowerLevelEventContent>("m.room.power_levels");

    [Obsolete("This method will be merged into GetNameAsync() in the future.")]
    public async Task<string> GetNameOrFallbackAsync(int maxMemberNames = 2) {
        try {
            var name = await GetNameAsync();
            if (!string.IsNullOrEmpty(name)) return name;
            throw new();
        }
        catch {
            try {
                var alias = await GetCanonicalAliasAsync();
                if (!string.IsNullOrWhiteSpace(alias?.Alias)) return alias.Alias;
                throw new Exception("No alias");
            }
            catch {
                try {
                    var members = GetMembersEnumerableAsync();
                    var memberList = new List<string>();
                    var memberCount = 0;
                    await foreach (var member in members)
                        if (member.StateKey != Homeserver.UserId)
                            memberList.Add(member.RawContent?["displayname"]?.GetValue<string>() ?? "");
                    memberCount = memberList.Count;
                    memberList.RemoveAll(string.IsNullOrWhiteSpace);
                    memberList = memberList.OrderBy(x => x).ToList();
                    if (memberList.Count > maxMemberNames)
                        return string.Join(", ", memberList.Take(maxMemberNames)) + " and " + (memberCount - maxMemberNames) + " others.";
                    return string.Join(", ", memberList);
                }
                catch {
                    return RoomId;
                }
            }
        }
    }

    public Task InviteUsersAsync(IEnumerable<string> users, string? reason = null, bool skipExisting = true) {
        var tasks = users.Select(x => InviteUserAsync(x, reason, skipExisting)).ToList();
        return Task.WhenAll(tasks);
    }

#endregion

#region Simple calls

    public async Task ForgetAsync() =>
        await Homeserver.ClientHttpClient.PostAsync($"/_matrix/client/v3/rooms/{RoomId}/forget", null);

    public async Task LeaveAsync(string? reason = null) =>
        await Homeserver.ClientHttpClient.PostAsJsonAsync($"/_matrix/client/v3/rooms/{RoomId}/leave", new {
            reason
        });

    public async Task KickAsync(string userId, string? reason = null) =>
        await Homeserver.ClientHttpClient.PostAsJsonAsync($"/_matrix/client/v3/rooms/{RoomId}/kick",
            new UserIdAndReason { UserId = userId, Reason = reason });

    public async Task BanAsync(string userId, string? reason = null) =>
        await Homeserver.ClientHttpClient.PostAsJsonAsync($"/_matrix/client/v3/rooms/{RoomId}/ban",
            new UserIdAndReason { UserId = userId, Reason = reason });

    public async Task UnbanAsync(string userId, string? reason = null) =>
        await Homeserver.ClientHttpClient.PostAsJsonAsync($"/_matrix/client/v3/rooms/{RoomId}/unban",
            new UserIdAndReason { UserId = userId, Reason = reason });

    public async Task InviteUserAsync(string userId, string? reason = null, bool skipExisting = true) {
        if (skipExisting && await GetStateOrNullAsync<RoomMemberEventContent>("m.room.member", userId) is not null)
            return;
        await Homeserver.ClientHttpClient.PostAsJsonAsync($"/_matrix/client/v3/rooms/{RoomId}/invite", new UserIdAndReason(userId, reason));
    }

#endregion

#region Events

    public async Task<EventIdResponse?> SendStateEventAsync(string eventType, object content) =>
        await (await Homeserver.ClientHttpClient.PutAsJsonAsync($"/_matrix/client/v3/rooms/{RoomId}/state/{eventType}", content))
            .Content.ReadFromJsonAsync<EventIdResponse>();

    public async Task<EventIdResponse?> SendStateEventAsync(string eventType, string stateKey, object content) =>
        await (await Homeserver.ClientHttpClient.PutAsJsonAsync($"/_matrix/client/v3/rooms/{RoomId}/state/{eventType.UrlEncode()}/{stateKey.UrlEncode()}", content))
            .Content.ReadFromJsonAsync<EventIdResponse>();

    public async Task<EventIdResponse> SendTimelineEventAsync(string eventType, TimelineEventContent content) {
        var res = await Homeserver.ClientHttpClient.PutAsJsonAsync(
            $"/_matrix/client/v3/rooms/{RoomId}/send/{eventType}/" + Guid.NewGuid(), content, new JsonSerializerOptions {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
        return await res.Content.ReadFromJsonAsync<EventIdResponse>() ?? throw new Exception("Failed to send event");
    }

    public async Task<EventIdResponse?> SendFileAsync(string fileName, Stream fileStream, string messageType = "m.file", string contentType = "application/octet-stream") {
        var url = await Homeserver.UploadFile(fileName, fileStream);
        var content = new RoomMessageEventContent() {
            MessageType = messageType,
            Url = url,
            Body = fileName,
            FileName = fileName,
            FileInfo = new RoomMessageEventContent.FileInfoStruct {
                Size = fileStream.Length,
                MimeType = contentType
            }
        };
        return await SendTimelineEventAsync("m.room.message", content);
    }

    public async Task<T?> GetRoomAccountDataAsync<T>(string key) {
        var res = await Homeserver.ClientHttpClient.GetAsync($"/_matrix/client/v3/user/{Homeserver.UserId}/rooms/{RoomId}/account_data/{key}");
        if (!res.IsSuccessStatusCode) {
            Console.WriteLine($"Failed to get room account data: {await res.Content.ReadAsStringAsync()}");
            throw new InvalidDataException($"Failed to get room account data: {await res.Content.ReadAsStringAsync()}");
        }

        return await res.Content.ReadFromJsonAsync<T>();
    }

    public async Task<T?> GetRoomAccountDataOrNullAsync<T>(string key) {
        try {
            return await GetRoomAccountDataAsync<T>(key);
        }
        catch (MatrixException e) {
            if (e.ErrorCode == "M_NOT_FOUND") return default;
            throw;
        }
    }

    public async Task SetRoomAccountDataAsync(string key, object data) {
        var res = await Homeserver.ClientHttpClient.PutAsJsonAsync($"/_matrix/client/v3/user/{Homeserver.UserId}/rooms/{RoomId}/account_data/{key}", data);
        if (!res.IsSuccessStatusCode) {
            Console.WriteLine($"Failed to set room account data: {await res.Content.ReadAsStringAsync()}");
            throw new InvalidDataException($"Failed to set room account data: {await res.Content.ReadAsStringAsync()}");
        }
    }

    public Task<StateEventResponse> GetEventAsync(string eventId) =>
        Homeserver.ClientHttpClient.GetFromJsonAsync<StateEventResponse>($"/_matrix/client/v3/rooms/{RoomId}/event/{eventId}");

    public async Task<EventIdResponse> RedactEventAsync(string eventToRedact, string? reason = null) {
        var data = new { reason };
        var url = $"/_matrix/client/v3/rooms/{RoomId}/redact/{eventToRedact}/{Guid.NewGuid().ToString()}";
        while (true) {
            try {
                return (await (await Homeserver.ClientHttpClient.PutAsJsonAsync(url, data)).Content.ReadFromJsonAsync<EventIdResponse>())!;
            } catch (MatrixException e) {
                if (e is { ErrorCode: MatrixException.ErrorCodes.M_FORBIDDEN }) throw;
                throw;
            }
        }
    }

#endregion

#region Utilities

    public async Task<Dictionary<string, List<string>>> GetMembersByHomeserverAsync(bool joinedOnly = true) {
        if (Homeserver is AuthenticatedHomeserverMxApiExtended mxaeHomeserver)
            return await mxaeHomeserver.ClientHttpClient.GetFromJsonAsync<Dictionary<string, List<string>>>(
                $"/_matrix/client/v3/rooms/{RoomId}/members_by_homeserver?joined_only={joinedOnly}");
        Dictionary<string, List<string>> roomHomeservers = new();
        var members = GetMembersEnumerableAsync();
        await foreach (var member in members) {
            var memberHs = member.StateKey.Split(':', 2)[1];
            roomHomeservers.TryAdd(memberHs, new List<string>());
            roomHomeservers[memberHs].Add(member.StateKey);
        }

        Console.WriteLine($"Finished processing {RoomId}");
        return roomHomeservers;
    }

#region Disband room

    public async Task PermanentlyBrickRoomAsync() {
        var states = GetFullStateAsync();
        List<string> stateTypeIgnore = new() {
            "m.room.create",
            "m.room.power_levels",
            "m.room.join_rules",
            "m.room.history_visibility",
            "m.room.guest_access",
            "m.room.member"
        };
        await foreach (var state in states) {
            if (state is null || state.RawContent is not { Count: > 0 }) continue;
            if (state.Type == "m.room.member" && state.StateKey != Homeserver.UserId)
                try {
                    await BanAsync(state.StateKey, "Disbanding room");
                }
                catch (MatrixException e) {
                    if (e.ErrorCode != "M_FORBIDDEN") throw;
                }

            if (stateTypeIgnore.Contains(state.Type)) continue;
            try {
                await SendStateEventAsync(state.Type, state.StateKey, new object());
            }
            catch { }
        }

        await LeaveAsync("Disbanded room");
    }

#endregion

#endregion

    public async IAsyncEnumerable<StateEventResponse> GetRelatedEventsAsync(string eventId, string? relationType = null, string? eventType = null, string? dir = "f",
        string? from = null, int? chunkLimit = 100, bool? recurse = null, string? to = null) {
        var path = $"/_matrix/client/v1/rooms/{RoomId}/relations/{HttpUtility.UrlEncode(eventId)}";
        if (!string.IsNullOrEmpty(relationType)) path += $"/{relationType}";
        if (!string.IsNullOrEmpty(eventType)) path += $"/{eventType}";

        var uri = new Uri(path, UriKind.Relative);
        if (dir == "b" || dir == "f") uri = uri.AddQuery("dir", dir);
        else if (!string.IsNullOrWhiteSpace(dir)) throw new ArgumentException("Invalid direction", nameof(dir));
        if (!string.IsNullOrEmpty(from)) uri = uri.AddQuery("from", from);
        if (chunkLimit is not null) uri = uri.AddQuery("limit", chunkLimit.Value.ToString());
        if (recurse is not null) uri = uri.AddQuery("recurse", recurse.Value.ToString());
        if (!string.IsNullOrEmpty(to)) uri = uri.AddQuery("to", to);

        // Console.WriteLine($"Getting related events from {uri}");
        var result = await Homeserver.ClientHttpClient.GetFromJsonAsync<RecursedBatchedChunkedStateEventResponse>(uri.ToString());
        while (result!.Chunk.Count > 0) {
            foreach (var resp in result.Chunk) {
                yield return resp;
            }

            if (result.NextBatch is null) break;
            result = await Homeserver.ClientHttpClient.GetFromJsonAsync<RecursedBatchedChunkedStateEventResponse>(uri.AddQuery("from", result.NextBatch).ToString());
        }
    }

    public readonly SpaceRoom AsSpace;
}

public class RoomIdResponse {
    [JsonPropertyName("room_id")]
    public string RoomId { get; set; } = null!;
}
