using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics;
using ArcaneLibs.Extensions;
using LibMatrix.Extensions;
using LibMatrix.Filters;
using LibMatrix.Homeservers;
using LibMatrix.Interfaces.Services;
using LibMatrix.Responses;
using Microsoft.Extensions.Logging;

namespace LibMatrix.Helpers;

public class SyncStateResolver(AuthenticatedHomeserverGeneric homeserver, ILogger? logger = null, IStorageProvider? storageProvider = null) {
    public string? Since { get; set; }
    public int Timeout { get; set; } = 30000;
    public string? SetPresence { get; set; } = "online";
    public SyncFilter? Filter { get; set; }
    public bool FullState { get; set; } = false;

    public SyncResponse? MergedState { get; set; }

    private SyncHelper _syncHelper = new(homeserver, logger, storageProvider);

    public async Task<(SyncResponse next, SyncResponse merged)> ContinueAsync(CancellationToken? cancellationToken = null) {
        // copy properties
        _syncHelper.Since = Since;
        _syncHelper.Timeout = Timeout;
        _syncHelper.SetPresence = SetPresence;
        _syncHelper.Filter = Filter;
        _syncHelper.FullState = FullState;

        var sync = await _syncHelper.SyncAsync(cancellationToken);
        if (sync is null) return await ContinueAsync(cancellationToken);

        if (MergedState is null) MergedState = sync;
        else MergedState = MergeSyncs(MergedState, sync);
        Since = sync.NextBatch;

        return (sync, MergedState);
    }

    public async Task OptimiseStore() {
        if (storageProvider is null) return;
        if (!await storageProvider.ObjectExistsAsync("init")) return;

        var totalSw = Stopwatch.StartNew();
        Console.Write("Optimising sync store...");
        var initLoadTask = storageProvider.LoadObjectAsync<SyncResponse>("init");
        var keys = (await storageProvider.GetAllKeysAsync()).Where(x=>!x.StartsWith("old/")).ToFrozenSet();
        var count = keys.Count - 1;
        Console.WriteLine($"Found {count} entries to optimise.");

        var merged = await initLoadTask;
        if (merged is null) return;
        if (!keys.Contains(merged.NextBatch)) {
            Console.WriteLine("Next response after initial sync is not present, not checkpointing!");
            return;
        }

        // We back up old entries
        var oldPath = $"old/{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        await storageProvider.MoveObjectAsync("init", $"{oldPath}/init");

        var moveTasks = new List<Task>();

        while (keys.Contains(merged.NextBatch)) {
            Console.Write($"Merging {merged.NextBatch}, {--count} remaining... ");
            var sw = Stopwatch.StartNew();
            var swt = Stopwatch.StartNew();
            var next = await storageProvider.LoadObjectAsync<SyncResponse>(merged.NextBatch);
            Console.Write($"Load {sw.GetElapsedAndRestart().TotalMilliseconds}ms... ");
            if (next is null || merged.NextBatch == next.NextBatch) break;

            Console.Write($"Check {sw.GetElapsedAndRestart().TotalMilliseconds}ms... ");
            // back up old entry
            moveTasks.Add(storageProvider.MoveObjectAsync(merged.NextBatch, $"{oldPath}/{merged.NextBatch}"));
            Console.Write($"Move {sw.GetElapsedAndRestart().TotalMilliseconds}ms... ");

            merged = MergeSyncs(merged, next);
            Console.Write($"Merge {sw.GetElapsedAndRestart().TotalMilliseconds}ms... ");
            Console.WriteLine($"Total {swt.Elapsed.TotalMilliseconds}ms");
            // Console.WriteLine($"Merged {merged.NextBatch}, {--count} remaining...");
        }

        await storageProvider.SaveObjectAsync("init", merged);
        await Task.WhenAll(moveTasks);
        
        Console.WriteLine($"Optimised store in {totalSw.Elapsed.TotalMilliseconds}ms");
    }

    /// <summary>
    /// Remove all but initial sync and last checkpoint
    /// </summary>
    public async Task RemoveOldSnapshots() {
        if(storageProvider is null) return;
        var sw = Stopwatch.StartNew();

        var map = await GetCheckpointMap();
        if (map is null) return;
        if(map.Count < 3) return;

        var toRemove = map.Keys.Skip(1).Take(map.Count - 2).ToList();
        Console.Write("Cleaning up old snapshots: ");
        foreach (var key in toRemove) {
            var path = $"old/{key}/init";
            if (await storageProvider?.ObjectExistsAsync(path)) {
                Console.Write($"{key}... ");
                await storageProvider?.DeleteObjectAsync(path);
            }
        }
        Console.WriteLine("Done!");
        Console.WriteLine($"Removed {toRemove.Count} old snapshots in {sw.Elapsed.TotalMilliseconds}ms");
    }

    public async Task UnrollOptimisedStore() {
        if (storageProvider is null) return;
        Console.WriteLine("WARNING: Unrolling sync store!");
    }

    public async Task SquashOptimisedStore(int targetCountPerCheckpoint) {
        Console.Write($"Balancing optimised store to {targetCountPerCheckpoint} per checkpoint...");
        var checkpoints = await GetCheckpointMap();
        if (checkpoints is null) return;

        Console.WriteLine(
            $" Stats: {checkpoints.Count} checkpoints with [{checkpoints.Min(x => x.Value.Count)} < ~{checkpoints.Average(x => x.Value.Count)} < {checkpoints.Max(x => x.Value.Count)}] entries");
        Console.WriteLine($"Found {checkpoints?.Count ?? 0} checkpoints.");
    }

    public async Task dev() {
        // var keys = (await storageProvider?.GetAllKeysAsync()).ToFrozenSet();
        // var times = new Dictionary<long, List<string>>();
        // var values = keys.Select(async x => Task.Run(async () => (x, await storageProvider?.LoadObjectAsync<SyncResponse>(x)))).ToAsyncEnumerable();
        // await foreach (var task in values) {
        //     var (key, data) = await task;
        //     if (data is null) continue;
        //     var derivTime = data.GetDerivedSyncTime();
        //     if (!times.ContainsKey(derivTime)) times[derivTime] = new();
        //     times[derivTime].Add(key);
        // }
        //
        // foreach (var (time, ckeys) in times.OrderBy(x => x.Key)) {
        //     Console.WriteLine($"{time}: {ckeys.Count} keys");
        // }

        // var map = await GetCheckpointMap();
        // if (map is null) return;
        //
        // var times = new Dictionary<long, List<string>>();
        // foreach (var (time, keys) in map) {
        //     Console.WriteLine($"{time}: {keys.Count} keys - calculating times");
        //     Dictionary<string, Task<SyncResponse?>?> tasks = keys.ToDictionary(x => x, x => storageProvider?.LoadObjectAsync<SyncResponse>(x));
        //     var nextKey = "init";
        //     long lastTime = 0;
        //     while (tasks.ContainsKey(nextKey)) {
        //         var data = await tasks[nextKey];
        //         if (data is null) break;
        //         var derivTime = data.GetDerivedSyncTime();
        //         if (derivTime == 0) derivTime = lastTime + 1;
        //         if (!times.ContainsKey(derivTime)) times[derivTime] = new();
        //         times[derivTime].Add(nextKey);
        //         lastTime = derivTime;
        //         nextKey = data.NextBatch;
        //     }
        // }
        //
        // foreach (var (time, ckeys) in times.OrderBy(x => x.Key)) {
        //     Console.WriteLine($"{time}: {ckeys.Count} keys");
        // }

        int i = 0;
        var sw = Stopwatch.StartNew();
        var hist = GetSerialisedHistory();
        await foreach (var (key, resp) in hist) {
            if (resp is null) continue;
            // Console.WriteLine($"[{++i}] {key} -> {resp.NextBatch} ({resp.GetDerivedSyncTime()})");
            i++;
        }
        Console.WriteLine($"Iterated {i} syncResponses in {sw.Elapsed}");
        Environment.Exit(0);
    }

    private async IAsyncEnumerable<(string key, SyncResponse? resp)> GetSerialisedHistory() {
        if (storageProvider is null) yield break;
        var map = await GetCheckpointMap();
        var currentRange = map.First();
        var nextKey = $"old/{map.First().Key}/init";
        var next = storageProvider.LoadObjectAsync<SyncResponse>(nextKey);
        while (true) {
            var data = await next;
            if (data is null) break;
            yield return (nextKey, data);
            if (currentRange.Value.Contains(data.NextBatch)) {
                nextKey = $"old/{currentRange.Key}/{data.NextBatch}";
            }
            else if (map.Any(x => x.Value.Contains(data.NextBatch))) {
                currentRange = map.First(x => x.Value.Contains(data.NextBatch));
                nextKey = $"old/{currentRange.Key}/{data.NextBatch}";
            }
            else if (await storageProvider.ObjectExistsAsync(data.NextBatch)) {
                nextKey = data.NextBatch;
            }
            else break;

            next = storageProvider.LoadObjectAsync<SyncResponse>(nextKey);
        }
    }

    public async Task<SyncResponse?> GetMergedUpTo(DateTime time) {
        if (storageProvider is null) return null;
        var unixTime = new DateTimeOffset(time.ToUniversalTime()).ToUnixTimeMilliseconds();
        var map = await GetCheckpointMap();
        if (map is null) return new();
        var stream = GetSerialisedHistory().GetAsyncEnumerator();
        SyncResponse? merged = await stream.MoveNextAsync() ? stream.Current.resp : null;

        if (merged.GetDerivedSyncTime() > unixTime) {
            Console.WriteLine("Initial sync is already past the target time!");
            Console.WriteLine($"CURRENT: {merged.GetDerivedSyncTime()} (UTC: {DateTimeOffset.FromUnixTimeMilliseconds(merged.GetDerivedSyncTime())})");
            Console.WriteLine($" TARGET: {unixTime} ({time.Kind}: {time}, UTC: {time.ToUniversalTime()})");
            return null;
        }

        while (await stream.MoveNextAsync()) {
            var (key, resp) = stream.Current;
            if (resp is null) continue;
            if (resp.GetDerivedSyncTime() > unixTime) break;
            merged = MergeSyncs(merged, resp);
        }
        
        return merged;
    }

    private async Task<ImmutableSortedDictionary<ulong, FrozenSet<string>>> GetCheckpointMap() {
        if (storageProvider is null) return null;
        var keys = (await storageProvider.GetAllKeysAsync()).ToFrozenSet();
        var map = new Dictionary<ulong, List<string>>();
        foreach (var key in keys) {
            if (!key.StartsWith("old/")) continue;
            var parts = key.Split('/');
            if (parts.Length < 3) continue;
            // if (!map.ContainsKey(parts[1])) map[parts[1]] = new();
            // map[parts[1]].Add(parts[2]);
            if (!ulong.TryParse(parts[1], out var checkpoint)) continue;
            if (!map.ContainsKey(checkpoint)) map[checkpoint] = new();
            map[checkpoint].Add(parts[2]);
        }

        return map.OrderBy(x => x.Key).ToImmutableSortedDictionary(x => x.Key, x => x.Value.ToFrozenSet());
    }

    private SyncResponse MergeSyncs(SyncResponse oldSync, SyncResponse newSync) {
        oldSync.NextBatch = newSync.NextBatch ?? oldSync.NextBatch;

        oldSync.AccountData ??= new EventList();
        oldSync.AccountData.Events ??= [];
        if (newSync.AccountData?.Events is not null)
            oldSync.AccountData.Events.MergeStateEventLists(newSync.AccountData?.Events ?? []);

        oldSync.Presence ??= new();
        oldSync.Presence.Events?.ReplaceBy(newSync.Presence?.Events ?? [], (oldState, newState) => oldState.Sender == newState.Sender && oldState.Type == newState.Type);

        oldSync.DeviceOneTimeKeysCount ??= new();
        if (newSync.DeviceOneTimeKeysCount is not null)
            foreach (var (key, value) in newSync.DeviceOneTimeKeysCount)
                oldSync.DeviceOneTimeKeysCount[key] = value;

        if (newSync.Rooms is not null)
            oldSync.Rooms = MergeRoomsDataStructure(oldSync.Rooms, newSync.Rooms);

        oldSync.ToDevice ??= new EventList();
        oldSync.ToDevice.Events ??= [];
        if (newSync.ToDevice?.Events is not null)
            oldSync.ToDevice.Events.MergeStateEventLists(newSync.ToDevice?.Events ?? []);

        oldSync.DeviceLists ??= new SyncResponse.DeviceListsDataStructure();
        oldSync.DeviceLists.Changed ??= [];
        oldSync.DeviceLists.Left ??= [];
        if (newSync.DeviceLists?.Changed is not null)
            foreach (var s in newSync.DeviceLists.Changed!) {
                oldSync.DeviceLists.Left.Remove(s);
                oldSync.DeviceLists.Changed.Add(s);
            }

        if (newSync.DeviceLists?.Left is not null)
            foreach (var s in newSync.DeviceLists.Left!) {
                oldSync.DeviceLists.Changed.Remove(s);
                oldSync.DeviceLists.Left.Add(s);
            }

        return oldSync;
    }

    private List<StateEventResponse>? MergePresenceEvents(List<StateEventResponse>? oldEvents, List<StateEventResponse>? newEvents) {
        if (oldEvents is null) return newEvents;
        if (newEvents is null) return oldEvents;

        foreach (var newEvent in newEvents) {
            oldEvents.RemoveAll(x => x.Sender == newEvent.Sender && x.Type == newEvent.Type);
            oldEvents.Add(newEvent);
        }

        return oldEvents;
    }

#region Merge rooms

    private SyncResponse.RoomsDataStructure MergeRoomsDataStructure(SyncResponse.RoomsDataStructure? oldState, SyncResponse.RoomsDataStructure newState) {
        if (oldState is null) return newState;
        oldState.Join ??= new Dictionary<string, SyncResponse.RoomsDataStructure.JoinedRoomDataStructure>();
        foreach (var (key, value) in newState.Join ?? new Dictionary<string, SyncResponse.RoomsDataStructure.JoinedRoomDataStructure>())
            if (!oldState.Join.ContainsKey(key)) oldState.Join[key] = value;
            else oldState.Join[key] = MergeJoinedRoomDataStructure(oldState.Join[key], value);

        oldState.Invite ??= new Dictionary<string, SyncResponse.RoomsDataStructure.InvitedRoomDataStructure>();
        foreach (var (key, value) in newState.Invite ?? new Dictionary<string, SyncResponse.RoomsDataStructure.InvitedRoomDataStructure>())
            if (!oldState.Invite.ContainsKey(key)) oldState.Invite[key] = value;
            else oldState.Invite[key] = MergeInvitedRoomDataStructure(oldState.Invite[key], value);

        oldState.Leave ??= new Dictionary<string, SyncResponse.RoomsDataStructure.LeftRoomDataStructure>();
        foreach (var (key, value) in newState.Leave ?? new Dictionary<string, SyncResponse.RoomsDataStructure.LeftRoomDataStructure>()) {
            if (!oldState.Leave.ContainsKey(key)) oldState.Leave[key] = value;
            else oldState.Leave[key] = MergeLeftRoomDataStructure(oldState.Leave[key], value);
            if (oldState.Invite.ContainsKey(key)) oldState.Invite.Remove(key);
            if (oldState.Join.ContainsKey(key)) oldState.Join.Remove(key);
        }

        return oldState;
    }

    private static SyncResponse.RoomsDataStructure.LeftRoomDataStructure MergeLeftRoomDataStructure(SyncResponse.RoomsDataStructure.LeftRoomDataStructure oldData,
        SyncResponse.RoomsDataStructure.LeftRoomDataStructure newData) {
        oldData.AccountData ??= new EventList();
        oldData.AccountData.Events ??= [];
        oldData.Timeline ??= new SyncResponse.RoomsDataStructure.JoinedRoomDataStructure.TimelineDataStructure();
        oldData.Timeline.Events ??= [];
        oldData.State ??= new EventList();
        oldData.State.Events ??= [];

        if (newData.AccountData?.Events is not null)
            oldData.AccountData.Events.MergeStateEventLists(newData.AccountData?.Events ?? []);

        if (newData.Timeline?.Events is not null)
            oldData.Timeline.Events.MergeStateEventLists(newData.Timeline?.Events ?? []);
        oldData.Timeline.Limited = newData.Timeline?.Limited ?? oldData.Timeline.Limited;
        oldData.Timeline.PrevBatch = newData.Timeline?.PrevBatch ?? oldData.Timeline.PrevBatch;

        if (newData.State?.Events is not null)
            oldData.State.Events.MergeStateEventLists(newData.State?.Events ?? []);

        return oldData;
    }

    private static SyncResponse.RoomsDataStructure.InvitedRoomDataStructure MergeInvitedRoomDataStructure(SyncResponse.RoomsDataStructure.InvitedRoomDataStructure oldData,
        SyncResponse.RoomsDataStructure.InvitedRoomDataStructure newData) {
        oldData.InviteState ??= new EventList();
        oldData.InviteState.Events ??= [];
        if (newData.InviteState?.Events is not null)
            oldData.InviteState.Events.MergeStateEventLists(newData.InviteState?.Events ?? []);

        return oldData;
    }

    private static SyncResponse.RoomsDataStructure.JoinedRoomDataStructure MergeJoinedRoomDataStructure(SyncResponse.RoomsDataStructure.JoinedRoomDataStructure oldData,
        SyncResponse.RoomsDataStructure.JoinedRoomDataStructure newData) {
        oldData.AccountData ??= new EventList();
        oldData.AccountData.Events ??= [];
        oldData.Timeline ??= new SyncResponse.RoomsDataStructure.JoinedRoomDataStructure.TimelineDataStructure();
        oldData.Timeline.Events ??= [];
        oldData.State ??= new EventList();
        oldData.State.Events ??= [];
        oldData.Ephemeral ??= new EventList();
        oldData.Ephemeral.Events ??= [];

        if (newData.AccountData?.Events is not null)
            oldData.AccountData.Events.MergeStateEventLists(newData.AccountData?.Events ?? []);

        if (newData.Timeline?.Events is not null)
            oldData.Timeline.Events.MergeStateEventLists(newData.Timeline?.Events ?? []);
        oldData.Timeline.Limited = newData.Timeline?.Limited ?? oldData.Timeline.Limited;
        oldData.Timeline.PrevBatch = newData.Timeline?.PrevBatch ?? oldData.Timeline.PrevBatch;

        if (newData.State?.Events is not null)
            oldData.State.Events.MergeStateEventLists(newData.State?.Events ?? []);

        if (newData.Ephemeral?.Events is not null)
            oldData.Ephemeral.Events.MergeStateEventLists(newData.Ephemeral?.Events ?? []);

        oldData.UnreadNotifications ??= new SyncResponse.RoomsDataStructure.JoinedRoomDataStructure.UnreadNotificationsDataStructure();
        oldData.UnreadNotifications.HighlightCount = newData.UnreadNotifications?.HighlightCount ?? oldData.UnreadNotifications.HighlightCount;
        oldData.UnreadNotifications.NotificationCount = newData.UnreadNotifications?.NotificationCount ?? oldData.UnreadNotifications.NotificationCount;

        oldData.Summary ??= new SyncResponse.RoomsDataStructure.JoinedRoomDataStructure.SummaryDataStructure {
            Heroes = newData.Summary?.Heroes ?? oldData.Summary.Heroes,
            JoinedMemberCount = newData.Summary?.JoinedMemberCount ?? oldData.Summary.JoinedMemberCount,
            InvitedMemberCount = newData.Summary?.InvitedMemberCount ?? oldData.Summary.InvitedMemberCount
        };
        oldData.Summary.Heroes = newData.Summary?.Heroes ?? oldData.Summary.Heroes;
        oldData.Summary.JoinedMemberCount = newData.Summary?.JoinedMemberCount ?? oldData.Summary.JoinedMemberCount;
        oldData.Summary.InvitedMemberCount = newData.Summary?.InvitedMemberCount ?? oldData.Summary.InvitedMemberCount;

        return oldData;
    }

#endregion
}