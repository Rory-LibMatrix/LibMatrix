using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using ArcaneLibs.Extensions;
using LibMatrix.EventTypes.Spec.State.RoomInfo;
using LibMatrix.HomeserverEmulator.Extensions;
using LibMatrix.HomeserverEmulator.Services;
using LibMatrix.Responses;
using Microsoft.AspNetCore.Mvc;

namespace LibMatrix.HomeserverEmulator.Controllers;

[ApiController]
[Route("/_matrix/client/{version}/")]
public class SyncController(ILogger<SyncController> logger, TokenService tokenService, UserStore userStore, RoomStore roomStore, HseConfiguration cfg) : ControllerBase {
    [HttpGet("sync")]
    [SuppressMessage("ReSharper.DPA", "DPA0011: High execution time of MVC action", Justification = "Endpoint is expected to wait until data is available or timeout.")]
    public async Task<SyncResponse> Sync([FromQuery] string? since = null, [FromQuery] int? timeout = 5000) {
        var sw = Stopwatch.StartNew();
        var token = tokenService.GetAccessToken(HttpContext);

        var user = await userStore.GetUserByToken(token);
        if (user == null)
            throw new MatrixException() {
                ErrorCode = "M_UNKNOWN_TOKEN",
                Error = "No such user"
            };
        var session = user.AccessTokens[token];
        UserStore.User.SessionInfo.UserSyncState newSyncState = new();

        SyncResponse syncResp;
        if (string.IsNullOrWhiteSpace(since) || !session.SyncStates.TryGetValue(since, out var syncState))
            syncResp = InitialSync(user, session);
        else {
            newSyncState = syncState.Clone();

            var newSyncToken = Guid.NewGuid().ToString();
            do {
                syncResp = IncrementalSync(user, session, syncState);
                syncResp.NextBatch = newSyncToken;
            } while (!await HasDataOrStall(syncResp) && sw.ElapsedMilliseconds < timeout);

            if (sw.ElapsedMilliseconds > timeout) {
                logger.LogTrace("Sync timed out after {Elapsed}", sw.Elapsed);
                return new() {
                    NextBatch = since
                };
            }
        }

        session.SyncStates[syncResp.NextBatch] = RecalculateSyncStates(newSyncState, syncResp);
        logger.LogTrace("Responding to sync after {totalElapsed}", sw.Elapsed);
        return syncResp;
    }

    private UserStore.User.SessionInfo.UserSyncState RecalculateSyncStates(UserStore.User.SessionInfo.UserSyncState newSyncState, SyncResponse sync) {
        logger.LogTrace("Updating sync state");
        var syncStateRecalcSw = Stopwatch.StartNew();
        foreach (var (roomId, roomData) in sync.Rooms?.Join ?? []) {
            if (!newSyncState.RoomPositions.ContainsKey(roomId))
                newSyncState.RoomPositions[roomId] = new();
            var state = newSyncState.RoomPositions[roomId];

            state.TimelinePosition += roomData.Timeline?.Events?.Count ?? 0;
            state.LastTimelineEventId = roomData.Timeline?.Events?.LastOrDefault()?.EventId ?? state.LastTimelineEventId;
            state.AccountDataPosition += roomData.AccountData?.Events?.Count ?? 0;
            state.Joined = true;
        }

        foreach (var (roomId, _) in sync.Rooms?.Invite ?? []) {
            if (!newSyncState.RoomPositions.ContainsKey(roomId))
                newSyncState.RoomPositions[roomId] = new() {
                    Joined = false
                };
        }

        foreach (var (roomId, _) in sync.Rooms?.Leave ?? []) {
            if (newSyncState.RoomPositions.ContainsKey(roomId))
                newSyncState.RoomPositions.Remove(roomId);
        }

        logger.LogTrace("Updated sync state in {Elapsed}", syncStateRecalcSw.Elapsed);

        return newSyncState;
    }

#region Initial Sync parts

    private SyncResponse InitialSync(UserStore.User user, UserStore.User.SessionInfo session) {
        var response = new SyncResponse() {
            NextBatch = Guid.NewGuid().ToString(),
            DeviceOneTimeKeysCount = new(),
            AccountData = new(events: user.AccountData.ToList())
        };

        session.SyncStates.Add(response.NextBatch, new());

        var rooms = roomStore._rooms.Where(x => x.State.Any(y => y.Type == "m.room.member" && y.StateKey == user.UserId)).ToList();
        foreach (var room in rooms) {
            response.Rooms ??= new();
            response.Rooms.Join ??= new();

            response.Rooms.Join[room.RoomId] = new() {
                State = new(room.State.ToList()),
                Timeline = new(events: room.Timeline.ToList(), limited: false),
                AccountData = new(room.AccountData.GetOrCreate(user.UserId, _ => []).ToList())
            };
        }

        return response;
    }

    private SyncResponse.RoomsDataStructure.JoinedRoomDataStructure GetInitialSyncRoomData(RoomStore.Room room, UserStore.User user) {
        return new() {
            State = new(room.State.ToList()),
            Timeline = new(room.Timeline.ToList(), false),
            AccountData = new(room.AccountData.GetOrCreate(user.UserId, _ => []).ToList())
        };
    }

#endregion

    private SyncResponse IncrementalSync(UserStore.User user, UserStore.User.SessionInfo session, UserStore.User.SessionInfo.UserSyncState syncState) {
        return new SyncResponse {
            Rooms = GetIncrementalSyncRooms(user, session, syncState)
        };
    }

#region Incremental Sync parts

    private SyncResponse.RoomsDataStructure GetIncrementalSyncRooms(UserStore.User user, UserStore.User.SessionInfo session, UserStore.User.SessionInfo.UserSyncState syncState) {
        SyncResponse.RoomsDataStructure data = new() {
            Join = [],
            Invite = [],
            Leave = []
        };

        // step 1: check previously synced rooms
        foreach (var (roomId, roomPosition) in syncState.RoomPositions) {
            var room = roomStore.GetRoomById(roomId);
            if (room == null) {
                // room no longer exists
                data.Leave[roomId] = new();
                continue;
            }

            if (roomPosition.Joined) {
                var newTimelineEvents = room.Timeline.Skip(roomPosition.TimelinePosition).ToList();
                var newAccountDataEvents = room.AccountData[user.UserId].Skip(roomPosition.AccountDataPosition).ToList();
                if (newTimelineEvents.Count == 0 && newAccountDataEvents.Count == 0) continue;
                data.Join[room.RoomId] = new() {
                    State = new(newTimelineEvents.GetCalculatedState()),
                    Timeline = new(newTimelineEvents, false)
                };
            }
        }

        if (data.Join.Count > 0) {
            logger.LogTrace("Found {count} updated rooms", data.Join.Count);
            return data;
        }

        // step 2: check newly joined rooms
        var untrackedRooms = roomStore._rooms.Where(r => !syncState.RoomPositions.ContainsKey(r.RoomId)).ToList();

        var allJoinedRooms = roomStore.GetRoomsByMember(user.UserId).ToArray();
        if (allJoinedRooms.Length == 0) return data;
        var rooms = Random.Shared.GetItems(allJoinedRooms, Math.Min(allJoinedRooms.Length, 50));
        foreach (var membership in rooms) {
            var membershipContent = membership.TypedContent as RoomMemberEventContent ??
                                    throw new InvalidOperationException("Membership event content is not RoomMemberEventContent");
            var room = roomStore.GetRoomById(membership.RoomId!);
            //handle leave
            if (syncState.RoomPositions.TryGetValue(membership.RoomId!, out var syncPosition)) {
                // logger.LogTrace("Found sync position {roomId} {value}", room.RoomId, syncPosition.ToJson(indent: false, ignoreNull: false));

                if (membershipContent.Membership == "join") {
                    var newTimelineEvents = room.Timeline.Skip(syncPosition.TimelinePosition).ToList();
                    var newAccountDataEvents = room.AccountData[user.UserId].Skip(syncPosition.AccountDataPosition).ToList();
                    if (newTimelineEvents.Count == 0 && newAccountDataEvents.Count == 0) continue;
                    data.Join[room.RoomId] = new() {
                        State = new(newTimelineEvents.GetCalculatedState()),
                        Timeline = new(newTimelineEvents, false)
                    };
                }
            }
            else {
                //syncPosisition = null
                if (membershipContent.Membership == "join") {
                    var joinData = data.Join[membership.RoomId!] = new() {
                        State = new(room.State.ToList()),
                        Timeline = new(events: room.Timeline.ToList(), limited: false),
                        AccountData = new(room.AccountData.GetOrCreate(user.UserId, _ => []).ToList())
                    };
                }
            }
        }

        //handle nonexistant rooms
        foreach (var roomId in syncState.RoomPositions.Keys) {
            if (!roomStore._rooms.Any(x => x.RoomId == roomId)) {
                data.Leave[roomId] = new();
                session.SyncStates[session.SyncStates.Last().Key].RoomPositions.Remove(roomId);
            }
        }

        return data;
    }

#endregion

    private bool HasData(SyncResponse resp) {
        return resp.Rooms?.Invite?.Count > 0 || resp.Rooms?.Join?.Count > 0 || resp.Rooms?.Leave?.Count > 0;
    }
    
    private async Task<bool> HasDataOrStall(SyncResponse resp) {
        // logger.LogTrace("Checking if sync response has data: {resp}", resp.ToJson(indent: false, ignoreNull: true));
        // if (resp.AccountData?.Events?.Count > 0) return true;
        // if (resp.Rooms?.Invite?.Count > 0) return true;
        // if (resp.Rooms?.Join?.Count > 0) return true;
        // if (resp.Rooms?.Leave?.Count > 0) return true;
        // if (resp.Presence?.Events?.Count > 0) return true;
        // if (resp.DeviceLists?.Changed?.Count > 0) return true;
        // if (resp.DeviceLists?.Left?.Count > 0) return true;
        // if (resp.ToDevice?.Events?.Count > 0) return true;
        //
        // var hasData =
        //     resp is not {
        //         AccountData: null or {
        //             Events: null or { Count: 0 }
        //         },
        //         Rooms: null or {
        //             Invite: null or { Count: 0 },
        //             Join: null or { Count: 0 },
        //             Leave: null or { Count: 0 }
        //         },
        //         Presence: null or {
        //             Events: null or { Count: 0 }
        //         },
        //         DeviceLists: null or {
        //             Changed: null or { Count: 0 },
        //             Left: null or { Count: 0 }
        //         },
        //         ToDevice: null or {
        //             Events: null or { Count: 0 }
        //         }
        //     };

        var hasData = resp is {
            AccountData: {
                Events: { Count: > 0 }
            }
        } or {
            Presence: {
                Events: { Count: > 0 }
            }
        } or {
            DeviceLists: {
                Changed: { Count: > 0 },
                Left: { Count: > 0 }
            }
        } or {
            ToDevice: {
                Events: { Count: > 0 }
            }
        } or {
            Rooms: {
                Invite: { Count: > 0 }
            } or {
                Join: { Count: > 0 }
            } or {
                Leave: { Count: > 0 }
            }
        };

        if (!hasData) {
            // hasData = 
        }

        if (!hasData) {
            // logger.LogDebug($"Sync response has no data, stalling for 1000ms: {resp.ToJson(indent: false, ignoreNull: true)}");
            await Task.Delay(10);
        }

        return hasData;
    }
}