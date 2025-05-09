using System.Diagnostics;
using LibMatrix.Responses;

namespace LibMatrix.Helpers.SyncProcessors;

public class SimpleSyncProcessors {
    public static SyncResponse? FillRoomIds(SyncResponse? resp) {
        var sw = Stopwatch.StartNew();
        if (resp is not { Rooms: not null }) return resp;
        if (resp.Rooms.Join is { Count: > 0 })
            Parallel.ForEach(resp.Rooms.Join, (roomEntry) => {
                var (id, data) = roomEntry;
                if (data.AccountData is { Events.Count: > 0 })
                    Parallel.ForEach(data.AccountData.Events, evt => evt.RoomId = id);
                if (data.Ephemeral is { Events.Count: > 0 })
                    Parallel.ForEach(data.Ephemeral.Events, evt => evt.RoomId = id);
                if (data.Timeline is { Events.Count: > 0 })
                    Parallel.ForEach(data.Timeline.Events, evt => evt.RoomId = id);
                if (data.State is { Events.Count: > 0 })
                    Parallel.ForEach(data.State.Events, evt => evt.RoomId = id);
                if (data.StateAfter is { Events.Count: > 0 })
                    Parallel.ForEach(data.StateAfter.Events, evt => evt.RoomId = id);
            });
        if (resp.Rooms.Leave is { Count: > 0 })
            Parallel.ForEach(resp.Rooms.Leave, (roomEntry) => {
                var (id, data) = roomEntry;
                if (data.AccountData is { Events.Count: > 0 })
                    Parallel.ForEach(data.AccountData.Events, evt => evt.RoomId = id);
                if (data.Timeline is { Events.Count: > 0 })
                    Parallel.ForEach(data.Timeline.Events, evt => evt.RoomId = id);
                if (data.State is { Events.Count: > 0 })
                    Parallel.ForEach(data.State.Events, evt => evt.RoomId = id);
                if (data.StateAfter is { Events.Count: > 0 })
                    Parallel.ForEach(data.StateAfter.Events, evt => evt.RoomId = id);
            });
        if (resp.Rooms.Invite is { Count: > 0 })
            Parallel.ForEach(resp.Rooms.Invite, (roomEntry) => {
                var (id, data) = roomEntry;
                if (data.InviteState is { Events.Count: > 0 })
                    Parallel.ForEach(data.InviteState.Events, evt => evt.RoomId = id);
            });

        Console.WriteLine($"SimpleSyncProcessors.FillRoomIds took {sw.Elapsed}");

        return resp;
    }
}