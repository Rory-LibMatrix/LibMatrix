using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
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

    public async Task OptimiseStore(Action<int, int>? progressCallback = null) {
        if (storageProvider is null) return;
        if (!await storageProvider.ObjectExistsAsync("init")) return;

        var totalSw = Stopwatch.StartNew();
        Console.Write("Optimising sync store...");
        var initLoadTask = storageProvider.LoadObjectAsync<SyncResponse>("init");
        var keys = (await storageProvider.GetAllKeysAsync()).Where(x => !x.StartsWith("old/")).ToFrozenSet();
        var count = keys.Count - 1;
        int total = count;
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

        Dictionary<string, Dictionary<string, TimeSpan>> traces = [];
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

            var trace = new Dictionary<string, TimeSpan>();
            traces[merged.NextBatch] = trace;
            merged = MergeSyncs(merged, next, trace);
            Console.Write($"Merge {sw.GetElapsedAndRestart().TotalMilliseconds}ms... ");
            Console.WriteLine($"Total {swt.Elapsed.TotalMilliseconds}ms");
            // Console.WriteLine($"Merged {merged.NextBatch}, {--count} remaining...");
            progressCallback?.Invoke(count, total);
        }

        var traceString = string.Join("\n", traces.Select(x => $"{x.Key}\t{x.Value.ToJson(indent: false)}"));
        var ms = new MemoryStream(Encoding.UTF8.GetBytes(traceString));
        await storageProvider.SaveStreamAsync($"traces/{oldPath}", ms);

        await storageProvider.SaveObjectAsync("init", merged);
        await Task.WhenAll(moveTasks);

        Console.WriteLine($"Optimised store in {totalSw.Elapsed.TotalMilliseconds}ms");
        Console.WriteLine($"Insertions: {EnumerableExtensions.insertions}, replacements: {EnumerableExtensions.replacements}");
    }

    /// <summary>
    /// Remove all but initial sync and last checkpoint
    /// </summary>
    public async Task RemoveOldSnapshots() {
        if (storageProvider is null) return;
        var sw = Stopwatch.StartNew();

        var map = await GetCheckpointMap();
        if (map is null) return;
        if (map.Count < 3) return;

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
            if (!ulong.TryParse(parts[1], out var checkpoint)) continue;
            if (!map.ContainsKey(checkpoint)) map[checkpoint] = new();
            map[checkpoint].Add(parts[2]);
        }

        return map.OrderBy(x => x.Key).ToImmutableSortedDictionary(x => x.Key, x => x.Value.ToFrozenSet());
    }

    private SyncResponse MergeSyncs(SyncResponse oldSync, SyncResponse newSync, Dictionary<string, TimeSpan>? trace = null) {
        var sw = Stopwatch.StartNew();
        oldSync.NextBatch = newSync.NextBatch ?? oldSync.NextBatch;

        oldSync.AccountData = MergeEventList(oldSync.AccountData, newSync.AccountData);
        trace?.Add("AccountData", sw.GetElapsedAndRestart());

        oldSync.Presence = MergeEventListBy(oldSync.Presence, newSync.Presence, (oldState, newState) => oldState.Sender == newState.Sender && oldState.Type == newState.Type);
        trace?.Add("Presence", sw.GetElapsedAndRestart());

        // TODO: can this be cleaned up?
        oldSync.DeviceOneTimeKeysCount ??= new();
        if (newSync.DeviceOneTimeKeysCount is not null)
            foreach (var (key, value) in newSync.DeviceOneTimeKeysCount)
                oldSync.DeviceOneTimeKeysCount[key] = value;
        trace?.Add("DeviceOneTimeKeysCount", sw.GetElapsedAndRestart());

        if (newSync.Rooms is not null)
            oldSync.Rooms = MergeRoomsDataStructure(oldSync.Rooms, newSync.Rooms, trace);
        trace?.Add("Rooms", sw.GetElapsedAndRestart());

        oldSync.ToDevice = MergeEventList(oldSync.ToDevice, newSync.ToDevice);
        trace?.Add("ToDevice", sw.GetElapsedAndRestart());

        oldSync.DeviceLists ??= new SyncResponse.DeviceListsDataStructure();
        oldSync.DeviceLists.Changed ??= [];
        oldSync.DeviceLists.Left ??= [];
        if (newSync.DeviceLists?.Changed is not null)
            foreach (var s in newSync.DeviceLists.Changed!) {
                oldSync.DeviceLists.Left.Remove(s);
                oldSync.DeviceLists.Changed.Add(s);
            }

        trace?.Add("DeviceLists.Changed", sw.GetElapsedAndRestart());

        if (newSync.DeviceLists?.Left is not null)
            foreach (var s in newSync.DeviceLists.Left!) {
                oldSync.DeviceLists.Changed.Remove(s);
                oldSync.DeviceLists.Left.Add(s);
            }

        trace?.Add("DeviceLists.Left", sw.GetElapsedAndRestart());

        return oldSync;
    }

#region Merge rooms

    private SyncResponse.RoomsDataStructure MergeRoomsDataStructure(SyncResponse.RoomsDataStructure? oldState, SyncResponse.RoomsDataStructure newState,
        Dictionary<string, TimeSpan>? trace) {
        var sw = Stopwatch.StartNew();
        if (oldState is null) return newState;

        if (newState.Join is { Count: > 0 })
            if (oldState.Join is null)
                oldState.Join = newState.Join;
            else
                foreach (var (key, value) in newState.Join)
                    if (!oldState.Join.TryAdd(key, value))
                        oldState.Join[key] = MergeJoinedRoomDataStructure(oldState.Join[key], value, trace);
        trace?.Add("MergeRoomsDataStructure.Join", sw.GetElapsedAndRestart());

        if (newState.Invite is { Count: > 0 })
            if (oldState.Invite is null)
                oldState.Invite = newState.Invite;
            else
                foreach (var (key, value) in newState.Invite)
                    if (!oldState.Invite.TryAdd(key, value))
                        oldState.Invite[key] = MergeInvitedRoomDataStructure(oldState.Invite[key], value, trace);
        trace?.Add("MergeRoomsDataStructure.Invite", sw.GetElapsedAndRestart());

        if (newState.Leave is { Count: > 0 })
            if (oldState.Leave is null)
                oldState.Leave = newState.Leave;
            else
                foreach (var (key, value) in newState.Leave) {
                    if (!oldState.Leave.TryAdd(key, value))
                        oldState.Leave[key] = MergeLeftRoomDataStructure(oldState.Leave[key], value, trace);
                    if (oldState.Invite?.ContainsKey(key) ?? false) oldState.Invite.Remove(key);
                    if (oldState.Join?.ContainsKey(key) ?? false) oldState.Join.Remove(key);
                }
        trace?.Add("MergeRoomsDataStructure.Leave", sw.GetElapsedAndRestart());

        return oldState;
    }

    private static SyncResponse.RoomsDataStructure.LeftRoomDataStructure MergeLeftRoomDataStructure(SyncResponse.RoomsDataStructure.LeftRoomDataStructure oldData,
        SyncResponse.RoomsDataStructure.LeftRoomDataStructure newData, Dictionary<string, TimeSpan>? trace) {
        var sw = Stopwatch.StartNew();

        oldData.AccountData = MergeEventList(oldData.AccountData, newData.AccountData);
        trace?.Add($"LeftRoomDataStructure.AccountData/{oldData.GetHashCode()}", sw.GetElapsedAndRestart());

        oldData.Timeline = AppendEventList(oldData.Timeline, newData.Timeline) as SyncResponse.RoomsDataStructure.JoinedRoomDataStructure.TimelineDataStructure
                           ?? throw new InvalidOperationException("Merged room timeline was not TimelineDataStructure");
        oldData.Timeline.Limited = newData.Timeline?.Limited ?? oldData.Timeline.Limited;
        oldData.Timeline.PrevBatch = newData.Timeline?.PrevBatch ?? oldData.Timeline.PrevBatch;
        trace?.Add($"LeftRoomDataStructure.Timeline/{oldData.GetHashCode()}", sw.GetElapsedAndRestart());

        oldData.State = MergeEventList(oldData.State, newData.State);
        trace?.Add($"LeftRoomDataStructure.State/{oldData.GetHashCode()}", sw.GetElapsedAndRestart());

        return oldData;
    }

    private static SyncResponse.RoomsDataStructure.InvitedRoomDataStructure MergeInvitedRoomDataStructure(SyncResponse.RoomsDataStructure.InvitedRoomDataStructure oldData,
        SyncResponse.RoomsDataStructure.InvitedRoomDataStructure newData, Dictionary<string, TimeSpan>? trace) {
        var sw = Stopwatch.StartNew();
        oldData.InviteState = MergeEventList(oldData.InviteState, newData.InviteState);
        trace?.Add($"InvitedRoomDataStructure.InviteState/{oldData.GetHashCode()}", sw.GetElapsedAndRestart());

        return oldData;
    }

    private static SyncResponse.RoomsDataStructure.JoinedRoomDataStructure MergeJoinedRoomDataStructure(SyncResponse.RoomsDataStructure.JoinedRoomDataStructure oldData,
        SyncResponse.RoomsDataStructure.JoinedRoomDataStructure newData, Dictionary<string, TimeSpan>? trace) {
        var sw = Stopwatch.StartNew();

        oldData.AccountData = MergeEventList(oldData.AccountData, newData.AccountData);
        trace?.Add($"JoinedRoomDataStructure.AccountData/{oldData.GetHashCode()}", sw.GetElapsedAndRestart());

        oldData.Timeline = AppendEventList(oldData.Timeline, newData.Timeline) as SyncResponse.RoomsDataStructure.JoinedRoomDataStructure.TimelineDataStructure
                           ?? throw new InvalidOperationException("Merged room timeline was not TimelineDataStructure");
        oldData.Timeline.Limited = newData.Timeline?.Limited ?? oldData.Timeline.Limited;
        oldData.Timeline.PrevBatch = newData.Timeline?.PrevBatch ?? oldData.Timeline.PrevBatch;
        trace?.Add($"JoinedRoomDataStructure.Timeline/{oldData.GetHashCode()}", sw.GetElapsedAndRestart());

        oldData.State = MergeEventList(oldData.State, newData.State);
        trace?.Add($"JoinedRoomDataStructure.State/{oldData.GetHashCode()}", sw.GetElapsedAndRestart());

        oldData.Ephemeral = MergeEventList(oldData.Ephemeral, newData.Ephemeral);
        trace?.Add($"JoinedRoomDataStructure.Ephemeral/{oldData.GetHashCode()}", sw.GetElapsedAndRestart());

        oldData.UnreadNotifications ??= new SyncResponse.RoomsDataStructure.JoinedRoomDataStructure.UnreadNotificationsDataStructure();
        oldData.UnreadNotifications.HighlightCount = newData.UnreadNotifications?.HighlightCount ?? oldData.UnreadNotifications.HighlightCount;
        oldData.UnreadNotifications.NotificationCount = newData.UnreadNotifications?.NotificationCount ?? oldData.UnreadNotifications.NotificationCount;
        trace?.Add($"JoinedRoom$DataStructure.UnreadNotifications/{oldData.GetHashCode()}", sw.GetElapsedAndRestart());

        if (oldData.Summary is null)
            oldData.Summary = newData.Summary;
        else {
            oldData.Summary.Heroes = newData.Summary?.Heroes ?? oldData.Summary.Heroes;
            oldData.Summary.JoinedMemberCount = newData.Summary?.JoinedMemberCount ?? oldData.Summary.JoinedMemberCount;
            oldData.Summary.InvitedMemberCount = newData.Summary?.InvitedMemberCount ?? oldData.Summary.InvitedMemberCount;
        }

        trace?.Add($"JoinedRoomDataStructure.Summary/{oldData.GetHashCode()}", sw.GetElapsedAndRestart());

        return oldData;
    }

#endregion

    private static EventList? MergeEventList(EventList? oldState, EventList? newState) {
        if (newState is null) return oldState;
        if (oldState is null) {
            return newState;
        }

        if (newState.Events is null) return oldState;
        if (oldState.Events is null) {
            oldState.Events = newState.Events;
            return oldState;
        }

        oldState.Events.MergeStateEventLists(newState.Events);
        return oldState;
    }

    private static EventList? MergeEventListBy(EventList? oldState, EventList? newState, Func<StateEventResponse, StateEventResponse, bool> comparer) {
        if (newState is null) return oldState;
        if (oldState is null) {
            return newState;
        }

        if (newState.Events is null) return oldState;
        if (oldState.Events is null) {
            oldState.Events = newState.Events;
            return oldState;
        }

        oldState.Events.ReplaceBy(newState.Events, comparer);
        return oldState;
    }

    private static EventList? AppendEventList(EventList? oldState, EventList? newState) {
        if (newState is null) return oldState;
        if (oldState is null) {
            return newState;
        }

        if (newState.Events is null) return oldState;
        if (oldState.Events is null) {
            oldState.Events = newState.Events;
            return oldState;
        }

        oldState.Events.AddRange(newState.Events);
        return oldState;
    }
}