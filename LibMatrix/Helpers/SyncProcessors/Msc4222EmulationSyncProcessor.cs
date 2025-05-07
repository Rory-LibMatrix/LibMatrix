using System.Diagnostics;
using ArcaneLibs.Extensions;
using LibMatrix.Homeservers;
using LibMatrix.Responses;

namespace LibMatrix.Helpers.SyncProcessors;

public class Msc4222EmulationSyncProcessor(AuthenticatedHomeserverGeneric homeserver) {
    private static bool StateEventsMatch(StateEventResponse a, StateEventResponse b) {
        return a.Type == b.Type && a.StateKey == b.StateKey;
    }

    private static bool StateEventIsNewer(StateEventResponse a, StateEventResponse b) {
        return StateEventsMatch(a, b) && a.OriginServerTs < b.OriginServerTs;
    }

    public async Task<SyncResponse?> EmulateMsc4222(SyncResponse? resp) {
        var sw = Stopwatch.StartNew();
        if (resp is null or { Rooms: null }) return resp;

        if (
            resp.Rooms.Join?.Any(x => x.Value.StateAfter is { Events.Count: > 0 }) == true
            || resp.Rooms.Leave?.Any(x => x.Value.StateAfter is { Events.Count: > 0 }) == true
        ) {
            Console.WriteLine($"Msc4222EmulationSyncProcessor.EmulateMsc4222 determined that no emulation is needed in {sw.Elapsed}");
            return resp;
        }

        var modified = false;
        List<Task<bool>> tasks = [];
        if (resp.Rooms is { Join.Count: > 0 }) {
            tasks.AddRange(resp.Rooms.Join.Select(ProcessJoinedRooms));
        }

        if (resp.Rooms is { Leave.Count: > 0 }) {
            tasks.AddRange(resp.Rooms.Leave.Select(ProcessLeftRooms));
        }

        var tasksEnum = tasks.ToAsyncEnumerable();
        await foreach (var wasModified in tasksEnum) {
            if (wasModified) {
                modified = true;
            }
        }

        Console.WriteLine($"Msc4222EmulationSyncProcessor.EmulateMsc4222 processed {resp.Rooms?.Join?.Count}/{resp.Rooms?.Leave?.Count} rooms in {sw.Elapsed}");
        if (modified)
            resp.Msc4222Method = SyncResponse.Msc4222SyncType.Emulated;

        return resp;
    }

    private async Task<bool> ProcessJoinedRooms(KeyValuePair<string, SyncResponse.RoomsDataStructure.JoinedRoomDataStructure> roomData) {
        var (roomId, data) = roomData;
        var room = homeserver.GetRoom(roomId);

        if (data.StateAfter is { Events.Count: > 0 }) {
            return false;
        }

        data.StateAfter = new() { Events = [] };

        data.StateAfter = new() {
            Events = []
        };

        var oldState = new List<StateEventResponse>();
        if (data.State is { Events.Count: > 0 }) {
            oldState.ReplaceBy(data.State.Events, StateEventIsNewer);
        }

        if (data.Timeline is { Limited: true }) {
            if (data.Timeline.Events != null)
                oldState.ReplaceBy(data.Timeline.Events, StateEventIsNewer);

            try {
                var timeline = await homeserver.GetRoom(roomId).GetMessagesAsync(limit: 250);
                if (timeline is { State.Count: > 0 }) {
                    oldState.ReplaceBy(timeline.State, StateEventIsNewer);
                }

                if (timeline is { Chunk.Count: > 0 }) {
                    oldState.ReplaceBy(timeline.Chunk.Where(x => x.StateKey != null), StateEventIsNewer);
                }
            }
            catch (Exception e) {
                Console.WriteLine($"Msc4222Emulation: Failed to get timeline for room {roomId}, state may be incomplete!\n{e}");
            }
        }

        oldState = oldState.DistinctBy(x => (x.Type, x.StateKey)).ToList();

        // Different order: we need oldState here to reduce the set
        try {
            data.StateAfter.Events = (await room.GetFullStateAsListAsync())
                // .Where(x=> oldState.Any(y => StateEventsMatch(x, y)))
                // .Join(oldState, x => (x.Type, x.StateKey), y => (y.Type, y.StateKey), (x, y) => x)
                .IntersectBy(oldState.Select(s => (s.Type, s.StateKey)), s => (s.Type, s.StateKey))
                .ToList();
            
            data.State = null;
            return true;
        }
        catch (Exception e) {
            Console.WriteLine($"Msc4222Emulation: Failed to get full state for room {roomId}, state may be incomplete!\n{e}");
        }

        var tasks = oldState
            .Select(async oldEvt => {
                try {
                    return await room.GetStateEventAsync(oldEvt.Type, oldEvt.StateKey!);
                }
                catch (Exception e) {
                    Console.WriteLine($"Msc4222Emulation: Failed to get state event {oldEvt.Type}/{oldEvt.StateKey} for room {roomId}, state may be incomplete!\n{e}");
                    return oldEvt;
                }
            });

        var tasksEnum = tasks.ToAsyncEnumerable();
        await foreach (var evt in tasksEnum) {
            data.StateAfter.Events.Add(evt);
        }

        data.State = null;

        return true;
    }

    private async Task<bool> ProcessLeftRooms(KeyValuePair<string, SyncResponse.RoomsDataStructure.LeftRoomDataStructure> roomData) {
        var (roomId, data) = roomData;
        var room = homeserver.GetRoom(roomId);

        if (data.StateAfter is { Events.Count: > 0 }) {
            return false;
        }

        data.StateAfter = new() {
            Events = []
        };

        try {
            data.StateAfter.Events = await room.GetFullStateAsListAsync();
            data.State = null;
            return true;
        }
        catch (Exception e) {
            Console.WriteLine($"Msc4222Emulation: Failed to get full state for room {roomId}, state may be incomplete!\n{e}");
        }

        var oldState = new List<StateEventResponse>();
        if (data.State is { Events.Count: > 0 }) {
            oldState.ReplaceBy(data.State.Events, StateEventIsNewer);
        }

        if (data.Timeline is { Limited: true }) {
            if (data.Timeline.Events != null)
                oldState.ReplaceBy(data.Timeline.Events, StateEventIsNewer);

            try {
                var timeline = await homeserver.GetRoom(roomId).GetMessagesAsync(limit: 250);
                if (timeline is { State.Count: > 0 }) {
                    oldState.ReplaceBy(timeline.State, StateEventIsNewer);
                }

                if (timeline is { Chunk.Count: > 0 }) {
                    oldState.ReplaceBy(timeline.Chunk.Where(x => x.StateKey != null), StateEventIsNewer);
                }
            }
            catch (Exception e) {
                Console.WriteLine($"Msc4222Emulation: Failed to get timeline for room {roomId}, state may be incomplete!\n{e}");
            }
        }

        oldState = oldState.DistinctBy(x => (x.Type, x.StateKey)).ToList();

        var tasks = oldState
            .Select(async oldEvt => {
                try {
                    return await room.GetStateEventAsync(oldEvt.Type, oldEvt.StateKey!);
                }
                catch (Exception e) {
                    Console.WriteLine($"Msc4222Emulation: Failed to get state event {oldEvt.Type}/{oldEvt.StateKey} for room {roomId}, state may be incomplete!\n{e}");
                    return oldEvt;
                }
            });

        var tasksEnum = tasks.ToAsyncEnumerable();
        await foreach (var evt in tasksEnum) {
            data.StateAfter.Events.Add(evt);
        }

        data.State = null;

        return true;
    }
}