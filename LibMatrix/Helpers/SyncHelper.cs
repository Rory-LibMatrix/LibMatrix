using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using ArcaneLibs.Collections;
using ArcaneLibs.Extensions;
using LibMatrix.Filters;
using LibMatrix.Homeservers;
using LibMatrix.Responses;
using LibMatrix.Utilities;
using Microsoft.Extensions.Logging;

namespace LibMatrix.Helpers;

public class SyncHelper(AuthenticatedHomeserverGeneric homeserver, ILogger? logger = null) {
    private SyncFilter? _filter;
    private string? _namedFilterName;
    private bool _filterIsDirty = false;
    private string? _filterId = null;

    public string? Since { get; set; }
    public int Timeout { get; set; } = 30000;
    public string? SetPresence { get; set; } = "online";

    public string? FilterId {
        get => _filterId;
        set {
            _filterId = value;
            _namedFilterName = null;
            _filter = null;
        }
    }

    public string? NamedFilterName {
        get => _namedFilterName;
        set {
            _namedFilterName = value;
            _filterIsDirty = true;
            _filterId = null;
        }
    }

    public SyncFilter? Filter {
        get => _filter;
        set {
            _filter = value;
            _filterIsDirty = true;
            _filterId = null;
            _namedFilterName = null;
        }
    }

    public bool FullState { get; set; }

    public bool IsInitialSync { get; set; } = true;

    public TimeSpan MinimumDelay { get; set; } = new(0);

    private async Task updateFilterAsync() {
        if (!string.IsNullOrWhiteSpace(NamedFilterName)) {
            _filterId = await homeserver.NamedCaches.FilterCache.GetOrSetValueAsync(NamedFilterName);
            if (_filterId is null)
                if (logger is null) Console.WriteLine($"Failed to get filter ID for named filter {NamedFilterName}");
                else logger.LogWarning("Failed to get filter ID for named filter {NamedFilterName}", NamedFilterName);
        }
        else if (Filter is not null)
            _filterId = (await homeserver.UploadFilterAsync(Filter)).FilterId;
        else _filterId = null;
    }

    public async Task<SyncResponse?> SyncAsync(CancellationToken? cancellationToken = null) {
        if (homeserver is null) {
            Console.WriteLine("Null passed as homeserver for SyncHelper!");
            throw new ArgumentNullException(nameof(homeserver), "Null passed as homeserver for SyncHelper!");
        }

        if (homeserver.ClientHttpClient is null) {
            Console.WriteLine("Homeserver for SyncHelper is not properly configured!");
            throw new ArgumentNullException(nameof(homeserver.ClientHttpClient), "Null passed as homeserver for SyncHelper!");
        }

        var sw = Stopwatch.StartNew();
        if (_filterIsDirty) await updateFilterAsync();

        var url = $"/_matrix/client/v3/sync?timeout={Timeout}&set_presence={SetPresence}&full_state={(FullState ? "true" : "false")}";
        if (!string.IsNullOrWhiteSpace(Since)) url += $"&since={Since}";
        if (_filterId is not null) url += $"&filter={_filterId}";

        // logger?.LogInformation("SyncHelper: Calling: {}", url);

        try {
            var httpResp = await homeserver.ClientHttpClient.GetAsync(url, cancellationToken ?? CancellationToken.None);
            if (httpResp is null) throw new NullReferenceException("Failed to send HTTP request");
            logger?.LogTrace("Got sync response: {} bytes, {} elapsed", httpResp.GetContentLength(), sw.Elapsed);
            var deserializeSw = Stopwatch.StartNew();
            var stream = await httpResp.Content.ReadAsStreamAsync();
            await using var seekableStream = new SeekableStream(stream);
            var resp = await JsonSerializer.DeserializeAsync<SyncResponse>(seekableStream, cancellationToken: cancellationToken ?? CancellationToken.None,
                jsonTypeInfo: SyncResponseSerializerContext.Default.SyncResponse);
            logger?.LogInformation("Deserialized sync response: {} bytes, {} elapsed, {} total", seekableStream.Position, deserializeSw.Elapsed, sw.Elapsed);
            var timeToWait = MinimumDelay.Subtract(sw.Elapsed);
            if (timeToWait.TotalMilliseconds > 0)
                await Task.Delay(timeToWait);
            return resp;
        }
        catch (TaskCanceledException) {
            Console.WriteLine("Sync cancelled!");
            logger?.LogWarning("Sync cancelled due to TaskCanceledException!");
        }
        catch (Exception e) {
            Console.WriteLine(e);
            logger?.LogError(e, "Failed to sync!\n{}", e.ToString());
        }

        return null;
    }

    public async IAsyncEnumerable<SyncResponse> EnumerateSyncAsync(CancellationToken? cancellationToken = null) {
        while (!cancellationToken?.IsCancellationRequested ?? true) {
            var sync = await SyncAsync(cancellationToken);
            if (sync is null) continue;
            if (!string.IsNullOrWhiteSpace(sync.NextBatch)) Since = sync.NextBatch;
            yield return sync;
        }
    }

    public async Task RunSyncLoopAsync(bool skipInitialSyncEvents = true, CancellationToken? cancellationToken = null) {
        // var sw = Stopwatch.StartNew();
        var emptyInitialSyncCount = 0;
        var syncCount = 0;
        var oldTimeout = Timeout;
        Timeout = 0;
        await foreach (var sync in EnumerateSyncAsync(cancellationToken)) {
            syncCount++;
            if (sync is {
                    AccountData: null or {
                        Events: null or { Count: 0 }
                    },
                    Rooms: null or {
                        Invite: null or { Count: 0 },
                        Join: null or { Count: 0 },
                        Leave: null or { Count: 0 }
                    },
                    Presence: null or {
                        Events: null or { Count: 0 }
                    },
                    DeviceLists: null or {
                        Changed: null or { Count: 0 },
                        Left: null or { Count: 0 }
                    },
                    ToDevice: null or {
                        Events: null or { Count: 0 }
                    }
                }) {
                emptyInitialSyncCount++;
                if (emptyInitialSyncCount >= 2) {
                    IsInitialSync = false;
                    Timeout = oldTimeout;
                }
            }
            else if (syncCount > 15 && IsInitialSync)
                Console.WriteLine(sync.ToJson(ignoreNull: true, indent: true));

            await RunSyncLoopCallbacksAsync(sync, IsInitialSync && skipInitialSyncEvents);
        }
    }

    private async Task RunSyncLoopCallbacksAsync(SyncResponse syncResponse, bool isInitialSync) {
        var tasks = SyncReceivedHandlers.Select(x => x(syncResponse)).ToList();
        await Task.WhenAll(tasks);

        if (syncResponse.AccountData is { Events.Count: > 0 })
            foreach (var accountDataEvent in syncResponse.AccountData.Events) {
                tasks = AccountDataReceivedHandlers.Select(x => x(accountDataEvent)).ToList();
                await Task.WhenAll(tasks);
            }

        await RunSyncLoopRoomCallbacksAsync(syncResponse, isInitialSync);
    }

    private async Task RunSyncLoopRoomCallbacksAsync(SyncResponse syncResponse, bool isInitialSync) {
        if (syncResponse.Rooms is { Invite.Count: > 0 })
            foreach (var roomInvite in syncResponse.Rooms.Invite) {
                var tasks = InviteReceivedHandlers.Select(x => x(roomInvite)).ToList();
                await Task.WhenAll(tasks);
            }

        if (isInitialSync) return;

        if (syncResponse.Rooms is { Join.Count: > 0 })
            foreach (var updatedRoom in syncResponse.Rooms.Join) {
                if (updatedRoom.Value.Timeline is null) continue;
                foreach (var stateEventResponse in updatedRoom.Value.Timeline.Events) {
                    stateEventResponse.RoomId = updatedRoom.Key;
                    var tasks = TimelineEventHandlers.Select(x => x(stateEventResponse)).ToList();
                    await Task.WhenAll(tasks);
                }
            }
    }

    /// <summary>
    /// Event fired when a sync response is received
    /// </summary>
    public List<Func<SyncResponse, Task>> SyncReceivedHandlers { get; } = new();

    /// <summary>
    /// Event fired when a room invite is received
    /// </summary>
    public List<Func<KeyValuePair<string, SyncResponse.RoomsDataStructure.InvitedRoomDataStructure>, Task>> InviteReceivedHandlers { get; } = new();

    /// <summary>
    /// Event fired when a timeline event is received
    /// </summary>
    public List<Func<StateEventResponse, Task>> TimelineEventHandlers { get; } = new();

    /// <summary>
    /// Event fired when an account data event is received
    /// </summary>
    public List<Func<StateEventResponse, Task>> AccountDataReceivedHandlers { get; } = new();
    
    private void Log(string message) {
        if (logger is null) Console.WriteLine(message);
        else logger.LogInformation(message);
    }
}