using System.Diagnostics;
using System.Text.Json.Nodes;
using ArcaneLibs;
using ArcaneLibs.Extensions;
using LibMatrix.EventTypes.Spec;
using LibMatrix.EventTypes.Spec.State.RoomInfo;
using LibMatrix.EventTypes.Spec.State.Space;
using LibMatrix.Helpers;
using LibMatrix.HomeserverEmulator.Extensions;
using LibMatrix.HomeserverEmulator.Services;
using LibMatrix.Responses;
using LibMatrix.Services;
using Microsoft.AspNetCore.Mvc;

namespace LibMatrix.HomeserverEmulator.Controllers.Rooms;

[ApiController]
[Route("/_matrix/client/{version}/rooms/{roomId}")]
public class RoomTimelineController(
    ILogger<RoomTimelineController> logger,
    TokenService tokenService,
    UserStore userStore,
    RoomStore roomStore,
    HseConfiguration hseConfig,
    HomeserverProviderService hsProvider) : ControllerBase {
    [HttpPut("send/{eventType}/{txnId}")]
    public async Task<EventIdResponse> SendMessage(string roomId, string eventType, string txnId, [FromBody] JsonObject content) {
        var token = tokenService.GetAccessToken(HttpContext);
        var user = await userStore.GetUserByToken(token);

        var room = roomStore.GetRoomById(roomId);
        if (room == null)
            throw new MatrixException() {
                ErrorCode = "M_NOT_FOUND",
                Error = "Room not found"
            };

        var evt = new StateEvent() {
            RawContent = content,
            Type = eventType
        }.ToStateEvent(user, room);

        if (evt.Type == RoomMessageEventContent.EventId && (evt.TypedContent as RoomMessageEventContent).Body.StartsWith("!hse"))
            _ = Task.Run(() => HandleHseCommand(evt, room, user));

        if (!room.JoinedMembers.Any(x => x.StateKey == user.UserId))
            throw new MatrixException() {
                ErrorCode = "M_FORBIDDEN",
                Error = "User is not in the room"
            };

        room.Timeline.Add(evt);

        return new() {
            EventId = evt.EventId
        };
    }

    [HttpGet("messages")]
    public async Task<MessagesResponse> GetMessages(string roomId, [FromQuery] string? from = null, [FromQuery] string? to = null, [FromQuery] int limit = 100,
        [FromQuery] string? dir = "b") {
        var token = tokenService.GetAccessToken(HttpContext);
        var user = await userStore.GetUserByToken(token);

        var room = roomStore.GetRoomById(roomId);
        if (room == null)
            throw new MatrixException() {
                ErrorCode = "M_NOT_FOUND",
                Error = "Room not found"
            };

        if (!room.JoinedMembers.Any(x => x.StateKey == user.UserId))
            throw new MatrixException() {
                ErrorCode = "M_FORBIDDEN",
                Error = "User is not in the room"
            };

        if (dir == "b") {
            var timeline = room.Timeline.TakeLast(limit).ToList();
            return new() {
                Start = timeline.First().EventId,
                End = timeline.Last().EventId,
                Chunk = timeline.AsEnumerable().Reverse().ToList(),
                State = timeline.GetCalculatedState()
            };
        }
        else if (dir == "f") {
            var timeline = room.Timeline.Take(limit).ToList();
            return new() {
                Start = timeline.First().EventId,
                End = room.Timeline.Last() == timeline.Last() ? null : timeline.Last().EventId,
                Chunk = timeline
            };
        }
        else
            throw new MatrixException() {
                ErrorCode = "M_BAD_REQUEST",
                Error = $"Invalid direction '{dir}'"
            };
    }

    [HttpGet("event/{eventId}")]
    public async Task<StateEventResponse> GetEvent(string roomId, string eventId) {
        var token = tokenService.GetAccessToken(HttpContext);
        var user = await userStore.GetUserByToken(token);

        var room = roomStore.GetRoomById(roomId);
        if (room == null)
            throw new MatrixException() {
                ErrorCode = "M_NOT_FOUND",
                Error = "Room not found"
            };

        if (!room.JoinedMembers.Any(x => x.StateKey == user.UserId))
            throw new MatrixException() {
                ErrorCode = "M_FORBIDDEN",
                Error = "User is not in the room"
            };

        var evt = room.Timeline.SingleOrDefault(x => x.EventId == eventId);
        if (evt == null)
            throw new MatrixException() {
                ErrorCode = "M_NOT_FOUND",
                Error = "Event not found"
            };

        return evt;
    }

    [HttpGet("relations/{eventId}")]
    public async Task<RecursedBatchedChunkedStateEventResponse> GetRelations(string roomId, string eventId, [FromQuery] string? dir = "b", [FromQuery] string? from = null,
        [FromQuery] int? limit = 100, [FromQuery] bool? recurse = false, [FromQuery] string? to = null) {
        var token = tokenService.GetAccessToken(HttpContext);
        var user = await userStore.GetUserByToken(token);

        var room = roomStore.GetRoomById(roomId);
        if (room == null)
            throw new MatrixException() {
                ErrorCode = "M_NOT_FOUND",
                Error = "Room not found"
            };

        if (!room.JoinedMembers.Any(x => x.StateKey == user.UserId))
            throw new MatrixException() {
                ErrorCode = "M_FORBIDDEN",
                Error = "User is not in the room"
            };

        var evt = room.Timeline.SingleOrDefault(x => x.EventId == eventId);
        if (evt == null)
            throw new MatrixException() {
                ErrorCode = "M_NOT_FOUND",
                Error = "Event not found"
            };

        var matchingEvents = await GetRelationsInternal(roomId, eventId, dir, from, limit, recurse, to);

        return new() {
            Chunk = matchingEvents.ToList()
        };
    }

    [HttpGet("relations/{eventId}/{relationType}")]
    public async Task<RecursedBatchedChunkedStateEventResponse> GetRelations(string roomId, string eventId, string relationType, [FromQuery] string? dir = "b",
        [FromQuery] string? from = null, [FromQuery] int? limit = 100, [FromQuery] bool? recurse = false, [FromQuery] string? to = null) {
        var token = tokenService.GetAccessToken(HttpContext);
        var user = await userStore.GetUserByToken(token);

        var room = roomStore.GetRoomById(roomId);
        if (room == null)
            throw new MatrixException() {
                ErrorCode = "M_NOT_FOUND",
                Error = "Room not found"
            };

        if (!room.JoinedMembers.Any(x => x.StateKey == user.UserId))
            throw new MatrixException() {
                ErrorCode = "M_FORBIDDEN",
                Error = "User is not in the room"
            };

        var evt = room.Timeline.SingleOrDefault(x => x.EventId == eventId);
        if (evt == null)
            throw new MatrixException() {
                ErrorCode = "M_NOT_FOUND",
                Error = "Event not found"
            };

        var matchingEvents = await GetRelationsInternal(roomId, eventId, dir, from, limit, recurse, to);

        return new() {
            Chunk = matchingEvents.ToList()
        };
    }

    [HttpGet("relations/{eventId}/{relationType}/{eventType}")]
    public async Task<RecursedBatchedChunkedStateEventResponse> GetRelations(string roomId, string eventId, string relationType, string eventType, [FromQuery] string? dir = "b",
        [FromQuery] string? from = null, [FromQuery] int? limit = 100, [FromQuery] bool? recurse = false, [FromQuery] string? to = null) {
        var token = tokenService.GetAccessToken(HttpContext);
        var user = await userStore.GetUserByToken(token);

        var room = roomStore.GetRoomById(roomId);
        if (room == null)
            throw new MatrixException() {
                ErrorCode = "M_NOT_FOUND",
                Error = "Room not found"
            };

        if (!room.JoinedMembers.Any(x => x.StateKey == user.UserId))
            throw new MatrixException() {
                ErrorCode = "M_FORBIDDEN",
                Error = "User is not in the room"
            };

        var evt = room.Timeline.SingleOrDefault(x => x.EventId == eventId);
        if (evt == null)
            throw new MatrixException() {
                ErrorCode = "M_NOT_FOUND",
                Error = "Event not found"
            };

        var matchingEvents = await GetRelationsInternal(roomId, eventId, dir, from, limit, recurse, to);

        return new() {
            Chunk = matchingEvents.ToList()
        };
    }

    private async Task<IEnumerable<StateEventResponse>> GetRelationsInternal(string roomId, string eventId, string dir, string? from, int? limit, bool? recurse, string? to) {
        var room = roomStore.GetRoomById(roomId);
        var evt = room.Timeline.SingleOrDefault(x => x.EventId == eventId);
        if (evt == null)
            throw new MatrixException() {
                ErrorCode = "M_NOT_FOUND",
                Error = "Event not found"
            };

        var relatedEvents = room.Timeline.Where(x => x.RawContent?["m.relates_to"]?["event_id"]?.GetValue<string>() == eventId);
        if (dir == "b") {
            relatedEvents = relatedEvents.TakeLast(limit ?? 100);
        }
        else if (dir == "f") {
            relatedEvents = relatedEvents.Take(limit ?? 100);
        }

        return relatedEvents;
    }

#region Commands

    private void InternalSendMessage(RoomStore.Room room, string content) {
        InternalSendMessage(room, new MessageBuilder().WithBody(content).Build());
    }

    private void InternalSendMessage(RoomStore.Room room, RoomMessageEventContent content) {
        logger.LogInformation("Sending internal message: {content}", content.Body);
        room.Timeline.Add(new StateEventResponse() {
            Type = RoomMessageEventContent.EventId,
            TypedContent = content,
            // Sender = $"@hse:{tokenService.GenerateServerName(HttpContext)}",
            Sender = $"@hse:{hseConfig.ServerName}",
            RoomId = room.RoomId,
            EventId = "$" + string.Join("", Random.Shared.GetItems("abcdefghijklmnopqrstuvwxyzABCDEFGHIJLKMNOPQRSTUVWXYZ0123456789".ToCharArray(), 100)),
            OriginServerTs = DateTimeOffset.Now.ToUnixTimeMilliseconds()
        });
    }

    private async Task HandleHseCommand(StateEventResponse evt, RoomStore.Room room, UserStore.User user) {
        logger.LogWarning("Handling HSE command for {0}: {1}", user.UserId, evt.RawContent.ToJson(false, true));
        try {
            var msgContent = evt.TypedContent as RoomMessageEventContent;
            var parts = msgContent.Body.Split('\n')[0].Split(" ");
            if (parts.Length < 2) return;

            var command = parts[1];
            Console.WriteLine($"Handling command {command}");
            switch (command) {
                case "import":
                    await HandleImportCommand(parts[2..], evt, room, user);
                    break;
                case "import-nheko-profiles":
                    await HandleImportNhekoProfilesCommand(parts[2..], evt, room, user);
                    break;
                case "clear-sync-states":
                    foreach (var (token, session) in user.AccessTokens) {
                        session.SyncStates.Clear();
                        InternalSendMessage(room, $"Cleared sync states for {token}.");
                    }

                    break;
                case "rsp": {
                    await Task.Delay(1000);
                    var chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJLKMNOPQRSTUVWXYZ0123456789";
                    for (int i = 0; i < 10000; i++) {
                        // await Task.Delay(100);
                        // InternalSendMessage(room, $"https://music.youtube.com/watch?v=90oZtyvavSk&i={i}");
                        var url = $"https://music.youtube.com/watch?v=";
                        for (int j = 0; j < 11; j++) {
                            url += chars[Random.Shared.Next(chars.Length)];
                        }

                        InternalSendMessage(room, url + "&i=" + i);
                        if (i % 5000 == 0 || i == 9999) {
                            // Thread.Sleep(1000);

                            do {
                                InternalSendMessage(room,
                                    $"Current GC memory: {Util.BytesToString(GC.GetTotalMemory(false))}, total process memory: {Util.BytesToString(Process.GetCurrentProcess().WorkingSet64)}");
                                GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
                                GC.WaitForPendingFinalizers();
                                InternalSendMessage(room,
                                    $"GC memory: {Util.BytesToString(GC.GetTotalMemory(false))}, total process memory: {Util.BytesToString(Process.GetCurrentProcess().WorkingSet64)}");
                                await Task.Delay(5000);
                            } while (Process.GetCurrentProcess().WorkingSet64 >= 1_024_000_000);
                        }
                    }

                    break;
                }
                case "genrooms": {
                    var sw = Stopwatch.StartNew();
                    var count = 1000;
                    for (int i = 0; i < count; i++) {
                        var crq = new CreateRoomRequest() {
                            Name = $"Test room {i}",
                            CreationContent = new() {
                                ["version"] = "11"
                            },
                            InitialState = []
                        };

                        if (Random.Shared.Next(100) > 75) {
                            crq.CreationContent["type"] = "m.space";
                            foreach (var item in Random.Shared.GetItems(roomStore._rooms.ToArray(), 50)) {
                                crq.InitialState!.Add(new StateEvent() {
                                    Type = "m.space.child",
                                    StateKey = item.RoomId,
                                    TypedContent = new SpaceChildEventContent() {
                                        Suggested = true,
                                        AutoJoin = true,
                                        Via = new List<string>()
                                    }
                                }.ToStateEvent(user, room));
                            }
                        }

                        var newRoom = roomStore.CreateRoom(crq);
                        newRoom.AddUser(user.UserId);
                    }

                    InternalSendMessage(room, $"Generated {count} new rooms in {sw.Elapsed}!");
                    break;
                }
                case "gc":
                    InternalSendMessage(room,
                        $"Current GC memory: {Util.BytesToString(GC.GetTotalMemory(false))}, total process memory: {Util.BytesToString(Process.GetCurrentProcess().WorkingSet64)}");
                    GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
                    GC.WaitForPendingFinalizers();
                    InternalSendMessage(room,
                        $"GC memory: {Util.BytesToString(GC.GetTotalMemory(false))}, total process memory: {Util.BytesToString(Process.GetCurrentProcess().WorkingSet64)}");
                    break;
                case "leave-all-rooms": {
                    var rooms = roomStore.GetRoomsByMember(user.UserId);
                    foreach (var memberEvt in rooms) {
                        var roomObj = roomStore.GetRoomById(memberEvt.RoomId);
                        roomObj.SetStateInternal(new() {
                            Type = RoomMemberEventContent.EventId,
                            StateKey = user.UserId,
                            TypedContent = new RoomMemberEventContent() {
                                Membership = "leave"
                            },
                        }, senderId: user.UserId);
                    }

                    break;
                }
                default:
                    InternalSendMessage(room, $"Command {command} not found!");
                    break;
            }
        }
        catch (Exception ex) {
            InternalSendMessage(room, $"An error occurred: {ex.Message}");
        }
    }

    private async Task HandleImportNhekoProfilesCommand(string[] args, StateEventResponse evt, RoomStore.Room room, UserStore.User user) {
        var msgContent = evt.TypedContent as RoomMessageEventContent;
        var parts = msgContent.Body.Split('\n');

        var data = parts.Where(x => x.Contains(@"\auth\access_token") || x.Contains(@"\auth\home_server")).ToList();
        if (data.Count < 2) {
            InternalSendMessage(room, "Invalid data.");
            return;
        }

        foreach (var line in data) {
            var processedLine = line.Replace("\\\\", "\\").Replace("\\_", "_");

            if (!processedLine.Contains(@"\auth\")) continue;
            var profile = processedLine.Split(@"\auth\")[0];
            if (!user.AuthorizedSessions.ContainsKey(profile))
                user.AuthorizedSessions.Add(profile, new());
            if (processedLine.Contains(@"home_server")) {
                var server = processedLine.Split('=')[1];
                user.AuthorizedSessions[profile].Homeserver = server;
            }
            else if (processedLine.Contains(@"access_token")) {
                var token = processedLine.Split('=')[1];
                user.AuthorizedSessions[profile].AccessToken = token;
            }
        }

        foreach (var (key, session) in user.AuthorizedSessions.ToList()) {
            if (string.IsNullOrWhiteSpace(session.Homeserver) || string.IsNullOrWhiteSpace(session.AccessToken)) {
                InternalSendMessage(room, $"Invalid profile {key}");
                user.AuthorizedSessions.Remove(key);
                continue;
            }

            InternalSendMessage(room, $"Got profile {key} with server {session.AccessToken}");
        }
    }

    private async Task HandleImportCommand(string[] args, StateEventResponse evt, RoomStore.Room room, UserStore.User user) {
        var roomId = args[0];
        var profile = args[1];

        InternalSendMessage(room, $"Importing room {roomId} through profile {profile}...");
        if (!user.AuthorizedSessions.ContainsKey(profile)) {
            InternalSendMessage(room, $"Profile {profile} not found.");
            return;
        }

        var userProfile = user.AuthorizedSessions[profile];

        InternalSendMessage(room, $"Authenticating with {userProfile.Homeserver}...");
        var hs = await hsProvider.GetAuthenticatedWithToken(userProfile.Homeserver, userProfile.AccessToken);
        InternalSendMessage(room, $"Authenticated with {userProfile.Homeserver}.");
        var hsRoom = hs.GetRoom(roomId);

        InternalSendMessage(room, $"Starting import...");
        var internalRoom = new RoomStore.Room(roomId);

        var timeline = hsRoom.GetManyMessagesAsync(limit: int.MaxValue, dir: "b", chunkSize: 100000);
        await foreach (var resp in timeline) {
            internalRoom.Timeline = new(resp.Chunk.AsEnumerable().Reverse().Concat(internalRoom.Timeline));
            InternalSendMessage(room, $"Imported {resp.Chunk.Count} events. Now up to a total of {internalRoom.Timeline.Count} events.");
        }

        InternalSendMessage(room, $"Import complete. Saving and inserting user");
        roomStore.AddRoom(internalRoom);
        internalRoom.AddUser(user.UserId);
        InternalSendMessage(room, $"Import complete. Room is now available.");
    }

#endregion
}