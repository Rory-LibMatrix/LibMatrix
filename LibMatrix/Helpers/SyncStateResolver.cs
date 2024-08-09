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
        // run sync or grab from storage if available
        // var sync = storageProvider != null && await storageProvider.ObjectExistsAsync(Since ?? "init")
        //     ? await storageProvider.LoadObjectAsync<SyncResponse>(Since ?? "init")
        //     : await _syncHelper.SyncAsync(cancellationToken);
        var sync = await _syncHelper.SyncAsync(cancellationToken);
        if (sync is null) return await ContinueAsync(cancellationToken);

        // if (storageProvider != null && !await storageProvider.ObjectExistsAsync(Since ?? "init"))
            // await storageProvider.SaveObjectAsync(Since ?? "init", sync);

        if (MergedState is null) MergedState = sync;
        else MergedState = MergeSyncs(MergedState, sync);
        Since = sync.NextBatch;

        return (sync, MergedState);
    }

    public async Task OptimiseStore() {
        if (storageProvider is null) return;

        var keys = await storageProvider.GetAllKeysAsync();
        var count = keys.Count - 2;
        var merged = await storageProvider.LoadObjectAsync<SyncResponse>("init");
        if (merged is null) return;

        while (keys.Contains(merged.NextBatch)) {
            var next = await storageProvider.LoadObjectAsync<SyncResponse>(merged.NextBatch);
            if (next is null) break;
            merged = MergeSyncs(merged, next);
            Console.WriteLine($"Merged {merged.NextBatch}, {--count} remaining...");
        }

        await storageProvider.SaveObjectAsync("merged", merged);

        Environment.Exit(0);
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