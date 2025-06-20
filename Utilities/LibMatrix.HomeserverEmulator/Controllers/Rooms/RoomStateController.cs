using System.Collections.Frozen;
using System.Text.Json.Nodes;
using LibMatrix.HomeserverEmulator.Extensions;
using LibMatrix.HomeserverEmulator.Services;
using LibMatrix.Responses;
using Microsoft.AspNetCore.Mvc;

namespace LibMatrix.HomeserverEmulator.Controllers.Rooms;

[ApiController]
[Route("/_matrix/client/{version}/rooms/{roomId}/state")]
public class RoomStateController(ILogger<RoomStateController> logger, TokenService tokenService, UserStore userStore, RoomStore roomStore) : ControllerBase {
    [HttpGet("")]
    public async Task<FrozenSet<StateEventResponse>> GetState(string roomId) {
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

        var room = roomStore.GetRoomById(roomId);
        if (room == null)
            throw new MatrixException() {
                ErrorCode = "M_NOT_FOUND",
                Error = "Room not found"
            };

        return room.State;
    }

    [HttpGet("{eventType}")]
    public async Task<object?> GetState(string roomId, string eventType, [FromQuery] string format = "client") {
        return await GetState(roomId, eventType, "", format);
    }

    [HttpGet("{eventType}/{stateKey}")]
    public async Task<object?> GetState(string roomId, string eventType, string stateKey, [FromQuery] string format = "client") {
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

        var room = roomStore.GetRoomById(roomId);
        if (room == null)
            throw new MatrixException() {
                ErrorCode = "M_NOT_FOUND",
                Error = "Room not found"
            };

        var stateEvent = room.State.FirstOrDefault(x => x.Type == eventType && x.StateKey == stateKey);
        if (stateEvent == null) {
            Console.WriteLine($"Event not found in room {roomId} matching {eventType}/{stateKey}");
            throw new MatrixException() {
                ErrorCode = "M_NOT_FOUND",
                Error = "Event not found"
            };
        }

        // return stateEvent;
        return format == "event" ? stateEvent : stateEvent.RawContent;
    }

    [HttpPut("{eventType}")]
    public async Task<EventIdResponse> SetState(string roomId, string eventType, [FromBody] JsonObject? request) {
        return await SetState(roomId, eventType, "", request);
    }

    [HttpPut("{eventType}/{stateKey}")]
    public async Task<EventIdResponse> SetState(string roomId, string eventType, string stateKey, [FromBody] JsonObject? request) {
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

        var room = roomStore.GetRoomById(roomId);
        if (room == null)
            throw new MatrixException() {
                ErrorCode = "M_NOT_FOUND",
                Error = "Room not found"
            };
        var evt = room.SetStateInternal(new StateEvent() { Type = eventType, StateKey = stateKey, RawContent = request }.ToStateEvent(user, room));
        evt.Type = eventType;
        evt.StateKey = stateKey;
        return new EventIdResponse() {
            EventId = evt.EventId ?? throw new InvalidOperationException("EventId is null")
        };
    }
}