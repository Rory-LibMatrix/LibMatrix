using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks.Dataflow;
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

    private async Task<SyncResponse?> LoadSyncResponse(string key) {
        if (storageProvider is null) ArgumentNullException.ThrowIfNull(storageProvider);
        var stream = await storageProvider.LoadStreamAsync(key);
        return JsonSerializer.Deserialize<SyncResponse>(stream!, SyncResponseSerializerContext.Default.SyncResponse);
    }

    private async Task SaveSyncResponse(string key, SyncResponse value) {
        ArgumentNullException.ThrowIfNull(storageProvider);
        var ms = new MemoryStream();
        await JsonSerializer.SerializeAsync(ms, value, SyncResponseSerializerContext.Default.SyncResponse);
        await storageProvider.SaveStreamAsync(key, ms);
    }

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
        else MergedState = await MergeSyncs(MergedState, sync);
        Since = sync.NextBatch;

        return (sync, MergedState);
    }

    // private async IAsyncEnumerable<List<SyncResponse>> MergeP() {

    // } 

    private async Task<SyncResponse?> OptimiseFrom(string start, Action<int, int>? progressCallback = null) {
        var a = GetSerializedUnoptimisedResponses(start);
        SyncResponse merged = null!;
        int iters = 0;
        var sw = Stopwatch.StartNew();
        await foreach (var (key, resp) in a) {
            if (resp is null) continue;
            iters++;
            // if (key == "init") _merged = resp;
            // else _merged = await MergeSyncs(_merged, resp);
            // Console.WriteLine($"{key} @ {resp.GetDerivedSyncTime()} -> {resp.NextBatch}");
        }

        Console.WriteLine($"OptimiseFrom {start} finished in {sw.Elapsed.TotalMilliseconds}ms with {iters} iterations");

        return merged;
    }

    private async Task<List<string>> GetSerializedUnoptimisedKeysParallel(string start = "init") {
        Dictionary<string, string> pairs = [];
        var unoptimisedKeys = (await storageProvider.GetAllKeysAsync()).Where(static x => !x.Contains('/')).ToFrozenSet();
        await Parallel.ForEachAsync(unoptimisedKeys, async (key, _) => {
            var data = await storageProvider.LoadObjectAsync<SyncResponse>(key, SyncResponseSerializerContext.Default.SyncResponse);
            if (data is null) return;
            lock (pairs)
                pairs.Add(key, data.NextBatch);
        });

        var serializedKeys = new List<string>();
        var currentKey = start;
        while (pairs.TryGetValue(currentKey, out var nextKey)) {
            serializedKeys.Add(currentKey);
            currentKey = nextKey;
        }

        return serializedKeys;
    }

    private async Task<SyncResponse> MergeRecursive(string[] keys, int depth = 0) {
        if (keys.Length > 10) {
            var newKeys = keys.Chunk((keys.Length / 2) + 1).ToArray();
            var (left, right) = (MergeRecursive(newKeys[0], depth + 1), MergeRecursive(newKeys[1], depth + 1));
            await Task.WhenAll(left, right);
            return await MergeSyncs(await left, await right);
        }

        // Console.WriteLine("Hit max depth: " + depth);
        SyncResponse merged = await LoadSyncResponse(keys[0]);
        foreach (var key in keys[1..]) {
            merged = await MergeSyncs(merged, await LoadSyncResponse(key));
        }

        return merged;
    }

    public async Task OptimiseStore(Action<int, int>? progressCallback = null) {
        if (storageProvider is null) return;
        if (!await storageProvider.ObjectExistsAsync("init")) return;
        //
        // {
        //     var a = GetSerializedUnoptimisedResponses();
        //     SyncResponse _merged = null!;
        //     await foreach (var (key, resp) in a) {
        //         if (resp is null) continue;
        //         // if (key == "init") _merged = resp;
        //         // else _merged = await MergeSyncs(_merged, resp);
        //         // Console.WriteLine($"{key} @ {resp.GetDerivedSyncTime()} -> {resp.NextBatch}");
        //     }
        //     Environment.Exit(0);
        // }

        {
            // List<string> serialisedKeys = new(4000000);
            // await foreach (var res in GetSerializedUnoptimisedResponses()) {
            // if (res.resp is null) continue;
            // serialisedKeys.Add(res.key);
            // if (serialisedKeys.Count % 1000 == 0) _ = Console.Out.WriteAsync($"{serialisedKeys.Count}\r");
            // }

            List<string> serialisedKeys = await GetSerializedUnoptimisedKeysParallel();

            await MergeRecursive(serialisedKeys.ToArray());

            // var chunkSize = serialisedKeys.Count / Environment.ProcessorCount;
            // var chunks = serialisedKeys.Chunk(chunkSize+1).Select(x => (x.First(), x.Length)).ToList();
            // Console.WriteLine($"Got {chunks.Count} chunks:");
            // foreach (var chunk in chunks) {
            //     Console.WriteLine($"Chunk {chunk.Item1} with length {chunk.Length}");
            // }
            // 
            // var mergeTasks = chunks.Select(async chunk => {
            //     var (startKey, length) = chunk;
            //     string currentKey = startKey;
            //     SyncResponse merged = await storageProvider.LoadObjectAsync<SyncResponse>(currentKey, SyncResponseSerializerContext.Default.SyncResponse);
            //     for (int i = 0; i < length; i++) {
            //         if (i % 1000 == 0) Console.Write($"{i}...     \r");
            //         var newData = await storageProvider.LoadObjectAsync<SyncResponse>(currentKey, SyncResponseSerializerContext.Default.SyncResponse);
            //         merged = await MergeSyncs(merged, newData);
            //         currentKey = merged.NextBatch;
            //     }
            // 
            //     return merged;
            // }).ToList();
            // 
            // var mergedResults = await Task.WhenAll(mergeTasks);
            // SyncResponse _merged = mergedResults[0];
            // foreach (var key in mergedResults[1..]) {
            //     _merged = await MergeSyncs(_merged, key);
            // }
        }

        Environment.Exit(0);

        return;

        var totalSw = Stopwatch.StartNew();
        Console.Write("Optimising sync store...");
        var initLoadTask = LoadSyncResponse("init");
        var keys = (await storageProvider.GetAllKeysAsync()).Where(static x => !x.StartsWith("old/")).ToFrozenSet();
        var count = keys.Count - 1;
        int total = count;
        Console.WriteLine($"Found {count} entries to optimise in {totalSw.Elapsed}.");

        var merged = await initLoadTask;
        if (merged is null) return;
        if (!keys.Contains(merged.NextBatch)) {
            Console.WriteLine("Next response after initial sync is not present, not checkpointing!");
            return;
        }

        // if (keys.Count > 100_000) {
        //     // batch data by core count
        //     
        //     return;
        // }

        // We back up old entries
        var oldPath = $"old/{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        await storageProvider.MoveObjectAsync("init", $"{oldPath}/init");

        var moveTasks = new List<Task>();

        Dictionary<string, Dictionary<string, TimeSpan>> traces = [];
        string[] loopTrace = new string[4];
        while (keys.Contains(merged.NextBatch)) {
            loopTrace[0] = $"Merging {merged.NextBatch}, {--count} remaining";
            var sw = Stopwatch.StartNew();
            var swt = Stopwatch.StartNew();
            var next = await LoadSyncResponse(merged.NextBatch);
            loopTrace[1] = $"Load {sw.GetElapsedAndRestart().TotalMilliseconds}ms";
            if (next is null || merged.NextBatch == next.NextBatch) break;

            // back up old entry
            moveTasks.Add(storageProvider.MoveObjectAsync(merged.NextBatch, $"{oldPath}/{merged.NextBatch}"));

            if (moveTasks.Count >= 250)
                moveTasks.RemoveAll(t => t.IsCompleted);

            if (moveTasks.Count >= 500) {
                Console.Write("Reached 500 moveTasks... ");
                moveTasks.RemoveAll(t => t.IsCompleted);
                Console.WriteLine($"{moveTasks.Count} remaining");
            }

            var trace = new Dictionary<string, TimeSpan>();
            traces[merged.NextBatch] = trace;
            merged = await MergeSyncs(merged, next, trace);
            loopTrace[2] = $"Merge {sw.GetElapsedAndRestart().TotalMilliseconds}ms";
            loopTrace[3] = $"Total {swt.Elapsed.TotalMilliseconds}ms";

            if (swt.ElapsedMilliseconds >= 25)
                Console.WriteLine(string.Join("... ", loopTrace));

            if (count % 50 == 0)
                progressCallback?.Invoke(count, total);
#if WRITE_TRACE
            var traceString = string.Join("\n", traces.Select(x => $"{x.Key}\t{x.Value.ToJson(indent: false, ignoreNull: true)}"));
            var ms = new MemoryStream(Encoding.UTF8.GetBytes(traceString));
            var traceSaveTask = storageProvider.SaveStreamAsync($"traces/{oldPath}", ms);
            var slowtraceString = string.Join("\n",
                traces
                    .Where(x=>x.Value.Max(y=>y.Value.TotalMilliseconds) >= 100)
                    .OrderBy(x=>x.Value.Max(y=>y.Value))
                    .Select(x => $"{x.Key}\t{x.Value.Where(y => y.Value.TotalMilliseconds >= 100).ToDictionary().ToJson(indent: false, ignoreNull: true)}"));
            var slowms = new MemoryStream(Encoding.UTF8.GetBytes(slowtraceString));
            var slowTraceSaveTask = storageProvider.SaveStreamAsync($"traces/{oldPath}-slow", slowms);
            var slow1straceString = string.Join("\n",
                traces
                    .Where(x=>x.Value.Max(y=>y.Value.TotalMilliseconds) >= 1000)
                    .OrderBy(x=>x.Value.Max(y=>y.Value))
                    .Select(x => $"{x.Key}\t{x.Value.Where(y => y.Value.TotalMilliseconds >= 1000).ToDictionary().ToJson(indent: false, ignoreNull: true)}"));
            var slow1sms = new MemoryStream(Encoding.UTF8.GetBytes(slow1straceString));
            var slow1sTraceSaveTask = storageProvider.SaveStreamAsync($"traces/{oldPath}-slow-1s", slow1sms);

            await Task.WhenAll(traceSaveTask, slowTraceSaveTask, slow1sTraceSaveTask);
#endif
        }

        await SaveSyncResponse("init", merged);
        await Task.WhenAll(moveTasks);

        Console.WriteLine($"Optimised store in {totalSw.Elapsed.TotalMilliseconds}ms");
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
            if (await storageProvider.ObjectExistsAsync(path)) {
                Console.Write($"{key}... ");
                await storageProvider.DeleteObjectAsync(path);
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

    private async IAsyncEnumerable<(string key, SyncResponse? resp)> GetSerializedUnoptimisedResponses(string since = "init") {
        if (storageProvider is null) yield break;
        var nextKey = since;
        var next = storageProvider.LoadObjectAsync<SyncResponse>(nextKey);
        while (true) {
            var data = await next;

            if (data is null) break;
            yield return (nextKey, data);
            if (await storageProvider.ObjectExistsAsync(data.NextBatch)) {
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
            merged = await MergeSyncs(merged, resp);
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

        return map.OrderBy(static x => x.Key).ToImmutableSortedDictionary(static x => x.Key, x => x.Value.ToFrozenSet());
    }

    private async Task<SyncResponse> MergeSyncs(SyncResponse oldSync, SyncResponse newSync, Dictionary<string, TimeSpan>? trace = null) {
        // var sw = Stopwatch.StartNew();
        oldSync.NextBatch = newSync.NextBatch;

        void Trace(string key, TimeSpan span) {
            if (trace is not null) {
                lock (trace)
                    trace.Add(key, span);
            }
        }

        var accountDataTask = Task.Run(() => {
            var sw = Stopwatch.StartNew();
            oldSync.AccountData = MergeEventList(oldSync.AccountData, newSync.AccountData);
            if (sw.ElapsedMilliseconds > 100) Console.WriteLine($"WARN: AccountData took {sw.ElapsedMilliseconds}ms");
            Trace("AccountData", sw.GetElapsedAndRestart());
        });

        var presenceTask = Task.Run(() => {
            var sw = Stopwatch.StartNew();
            oldSync.Presence = MergeEventListBy(oldSync.Presence, newSync.Presence,
                static (oldState, newState) => oldState.Sender == newState.Sender && oldState.Type == newState.Type);
            if (sw.ElapsedMilliseconds > 100) Console.WriteLine($"WARN: Presence took {sw.ElapsedMilliseconds}ms");
            Trace("Presence", sw.GetElapsedAndRestart());
        });

        {
            var sw = Stopwatch.StartNew();
            // TODO: can this be cleaned up?
            oldSync.DeviceOneTimeKeysCount ??= new();
            if (newSync.DeviceOneTimeKeysCount is not null)
                foreach (var (key, value) in newSync.DeviceOneTimeKeysCount)
                    oldSync.DeviceOneTimeKeysCount[key] = value;
            if (sw.ElapsedMilliseconds > 100) Console.WriteLine($"WARN: DeviceOneTimeKeysCount took {sw.ElapsedMilliseconds}ms");
            Trace("DeviceOneTimeKeysCount", sw.GetElapsedAndRestart());
        }

        var roomsTask = Task.Run(() => {
            var sw = Stopwatch.StartNew();
            if (newSync.Rooms is not null)
                oldSync.Rooms = MergeRoomsDataStructure(oldSync.Rooms, newSync.Rooms, Trace);
            if (sw.ElapsedMilliseconds > 100) Console.WriteLine($"WARN: Rooms took {sw.ElapsedMilliseconds}ms");
            Trace("Rooms", sw.GetElapsedAndRestart());
        });

        var toDeviceTask = Task.Run(() => {
            var sw = Stopwatch.StartNew();
            // oldSync.ToDevice = MergeEventList(oldSync.ToDevice, newSync.ToDevice);
            oldSync.ToDevice = AppendEventList(oldSync.ToDevice, newSync.ToDevice);
            if (sw.ElapsedMilliseconds > 100) Console.WriteLine($"WARN: ToDevice took {sw.ElapsedMilliseconds}ms");
            Trace("ToDevice", sw.GetElapsedAndRestart());
        });

        var deviceListsTask = Task.Run(() => {
            var sw = Stopwatch.StartNew();
            oldSync.DeviceLists ??= new SyncResponse.DeviceListsDataStructure();
            oldSync.DeviceLists.Changed ??= [];
            oldSync.DeviceLists.Left ??= [];
            if (newSync.DeviceLists?.Changed is not null)
                foreach (var s in newSync.DeviceLists.Changed!) {
                    oldSync.DeviceLists.Left.Remove(s);
                    oldSync.DeviceLists.Changed.Add(s);
                }

            if (sw.ElapsedMilliseconds > 100) Console.WriteLine($"WARN: DeviceLists.Changed took {sw.ElapsedMilliseconds}ms");
            Trace("DeviceLists.Changed", sw.GetElapsedAndRestart());

            if (newSync.DeviceLists?.Left is not null)
                foreach (var s in newSync.DeviceLists.Left!) {
                    oldSync.DeviceLists.Changed.Remove(s);
                    oldSync.DeviceLists.Left.Add(s);
                }

            if (sw.ElapsedMilliseconds > 100) Console.WriteLine($"WARN: DeviceLists.Left took {sw.ElapsedMilliseconds}ms");
            Trace("DeviceLists.Left", sw.GetElapsedAndRestart());
        });

        await Task.WhenAll(accountDataTask, presenceTask, roomsTask, toDeviceTask, deviceListsTask);

        return oldSync;
    }

#region Merge rooms

    private SyncResponse.RoomsDataStructure MergeRoomsDataStructure(SyncResponse.RoomsDataStructure? oldState, SyncResponse.RoomsDataStructure newState,
        Action<string, TimeSpan> trace) {
        var sw = Stopwatch.StartNew();
        if (oldState is null) return newState;

        if (newState.Join is { Count: > 0 })
            if (oldState.Join is null)
                oldState.Join = newState.Join;
            else
                foreach (var (key, value) in newState.Join)
                    if (!oldState.Join.TryAdd(key, value))
                        oldState.Join[key] = MergeJoinedRoomDataStructure(oldState.Join[key], value, key, trace);
        if (sw.ElapsedMilliseconds > 100) Console.WriteLine($"WARN: MergeRoomsDataStructure.Join took {sw.ElapsedMilliseconds}ms");
        trace("MergeRoomsDataStructure.Join", sw.GetElapsedAndRestart());

        if (newState.Invite is { Count: > 0 })
            if (oldState.Invite is null)
                oldState.Invite = newState.Invite;
            else
                foreach (var (key, value) in newState.Invite)
                    if (!oldState.Invite.TryAdd(key, value))
                        oldState.Invite[key] = MergeInvitedRoomDataStructure(oldState.Invite[key], value, key, trace);
        if (sw.ElapsedMilliseconds > 100) Console.WriteLine($"WARN: MergeRoomsDataStructure.Invite took {sw.ElapsedMilliseconds}ms");
        trace("MergeRoomsDataStructure.Invite", sw.GetElapsedAndRestart());

        if (newState.Leave is { Count: > 0 })
            if (oldState.Leave is null)
                oldState.Leave = newState.Leave;
            else
                foreach (var (key, value) in newState.Leave) {
                    if (!oldState.Leave.TryAdd(key, value))
                        oldState.Leave[key] = MergeLeftRoomDataStructure(oldState.Leave[key], value, key, trace);
                    if (oldState.Invite?.ContainsKey(key) ?? false) oldState.Invite.Remove(key);
                    if (oldState.Join?.ContainsKey(key) ?? false) oldState.Join.Remove(key);
                }

        if (sw.ElapsedMilliseconds > 100) Console.WriteLine($"WARN: MergeRoomsDataStructure.Leave took {sw.ElapsedMilliseconds}ms");
        trace("MergeRoomsDataStructure.Leave", sw.GetElapsedAndRestart());

        return oldState;
    }

    private static SyncResponse.RoomsDataStructure.LeftRoomDataStructure MergeLeftRoomDataStructure(SyncResponse.RoomsDataStructure.LeftRoomDataStructure oldData,
        SyncResponse.RoomsDataStructure.LeftRoomDataStructure newData, string roomId, Action<string, TimeSpan> trace) {
        var sw = Stopwatch.StartNew();

        oldData.AccountData = MergeEventList(oldData.AccountData, newData.AccountData);
        trace($"LeftRoomDataStructure.AccountData/{roomId}", sw.GetElapsedAndRestart());

        oldData.Timeline = AppendEventList(oldData.Timeline, newData.Timeline) as SyncResponse.RoomsDataStructure.JoinedRoomDataStructure.TimelineDataStructure
                           ?? throw new InvalidOperationException("Merged room timeline was not TimelineDataStructure");
        oldData.Timeline.Limited = newData.Timeline?.Limited ?? oldData.Timeline.Limited;
        oldData.Timeline.PrevBatch = newData.Timeline?.PrevBatch ?? oldData.Timeline.PrevBatch;
        trace($"LeftRoomDataStructure.Timeline/{roomId}", sw.GetElapsedAndRestart());

        oldData.State = MergeEventList(oldData.State, newData.State);
        trace($"LeftRoomDataStructure.State/{roomId}", sw.GetElapsedAndRestart());

        return oldData;
    }

    private static SyncResponse.RoomsDataStructure.InvitedRoomDataStructure MergeInvitedRoomDataStructure(SyncResponse.RoomsDataStructure.InvitedRoomDataStructure oldData,
        SyncResponse.RoomsDataStructure.InvitedRoomDataStructure newData, string roomId, Action<string, TimeSpan> trace) {
        var sw = Stopwatch.StartNew();
        oldData.InviteState = MergeEventList(oldData.InviteState, newData.InviteState);
        if (sw.ElapsedMilliseconds > 100) Console.WriteLine($"WARN: MergeInvitedRoomDataStructure.InviteState took {sw.ElapsedMilliseconds}ms for {roomId}");
        trace($"InvitedRoomDataStructure.InviteState/{roomId}", sw.GetElapsedAndRestart());

        return oldData;
    }

    private static SyncResponse.RoomsDataStructure.JoinedRoomDataStructure MergeJoinedRoomDataStructure(SyncResponse.RoomsDataStructure.JoinedRoomDataStructure oldData,
        SyncResponse.RoomsDataStructure.JoinedRoomDataStructure newData, string roomId, Action<string, TimeSpan> trace) {
        var sw = Stopwatch.StartNew();

        oldData.AccountData = MergeEventList(oldData.AccountData, newData.AccountData);

        if (sw.ElapsedMilliseconds > 100) Console.WriteLine($"WARN: MergeJoinedRoomDataStructure.AccountData took {sw.ElapsedMilliseconds}ms for {roomId}");
        trace($"JoinedRoomDataStructure.AccountData/{roomId}", sw.GetElapsedAndRestart());

        oldData.Timeline = AppendEventList(oldData.Timeline, newData.Timeline) as SyncResponse.RoomsDataStructure.JoinedRoomDataStructure.TimelineDataStructure
                           ?? throw new InvalidOperationException("Merged room timeline was not TimelineDataStructure");
        oldData.Timeline.Limited = newData.Timeline?.Limited ?? oldData.Timeline.Limited;
        oldData.Timeline.PrevBatch = newData.Timeline?.PrevBatch ?? oldData.Timeline.PrevBatch;

        if (sw.ElapsedMilliseconds > 100) Console.WriteLine($"WARN: MergeJoinedRoomDataStructure.Timeline took {sw.ElapsedMilliseconds}ms for {roomId}");
        trace($"JoinedRoomDataStructure.Timeline/{roomId}", sw.GetElapsedAndRestart());

        oldData.State = MergeEventList(oldData.State, newData.State);

        if (sw.ElapsedMilliseconds > 100) Console.WriteLine($"WARN: MergeJoinedRoomDataStructure.State took {sw.ElapsedMilliseconds}ms for {roomId}");
        trace($"JoinedRoomDataStructure.State/{roomId}", sw.GetElapsedAndRestart());

        oldData.Ephemeral = MergeEventList(oldData.Ephemeral, newData.Ephemeral);

        if (sw.ElapsedMilliseconds > 100) Console.WriteLine($"WARN: MergeJoinedRoomDataStructure.Ephemeral took {sw.ElapsedMilliseconds}ms for {roomId}");
        trace($"JoinedRoomDataStructure.Ephemeral/{roomId}", sw.GetElapsedAndRestart());

        oldData.UnreadNotifications ??= new SyncResponse.RoomsDataStructure.JoinedRoomDataStructure.UnreadNotificationsDataStructure();
        oldData.UnreadNotifications.HighlightCount = newData.UnreadNotifications?.HighlightCount ?? oldData.UnreadNotifications.HighlightCount;
        oldData.UnreadNotifications.NotificationCount = newData.UnreadNotifications?.NotificationCount ?? oldData.UnreadNotifications.NotificationCount;

        if (sw.ElapsedMilliseconds > 100) Console.WriteLine($"WARN: MergeJoinedRoomDataStructure.UnreadNotifications took {sw.ElapsedMilliseconds}ms for {roomId}");
        trace($"JoinedRoom$DataStructure.UnreadNotifications/{roomId}", sw.GetElapsedAndRestart());

        if (oldData.Summary is null)
            oldData.Summary = newData.Summary;
        else {
            oldData.Summary.Heroes = newData.Summary?.Heroes ?? oldData.Summary.Heroes;
            oldData.Summary.JoinedMemberCount = newData.Summary?.JoinedMemberCount ?? oldData.Summary.JoinedMemberCount;
            oldData.Summary.InvitedMemberCount = newData.Summary?.InvitedMemberCount ?? oldData.Summary.InvitedMemberCount;
        }

        if (sw.ElapsedMilliseconds > 100) Console.WriteLine($"WARN: MergeJoinedRoomDataStructure.Summary took {sw.ElapsedMilliseconds}ms for {roomId}");
        trace($"JoinedRoomDataStructure.Summary/{roomId}", sw.GetElapsedAndRestart());

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

        // oldState.Events.MergeStateEventLists(newState.Events);
        oldState = MergeEventListBy(oldState, newState, static (oldEvt, newEvt) => oldEvt.Type == newEvt.Type && oldEvt.StateKey == newEvt.StateKey);
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