using LibMatrix.Extensions;
using LibMatrix.Filters;
using LibMatrix.Homeservers;
using LibMatrix.Responses;
using Microsoft.Extensions.Logging;

namespace LibMatrix.Helpers;

public class SyncStateResolver(AuthenticatedHomeserverGeneric homeserver, ILogger? logger = null) {
    public string? Since { get; set; }
    public int Timeout { get; set; } = 30000;
    public string? SetPresence { get; set; } = "online";
    public SyncFilter? Filter { get; set; }
    public bool FullState { get; set; } = false;

    public SyncResponse? MergedState { get; set; }

    private SyncHelper _syncHelper = new(homeserver, logger);

    public async Task<(SyncResponse next, SyncResponse merged)> ContinueAsync(CancellationToken? cancellationToken = null) {
        // copy properties
        _syncHelper.Since = Since;
        _syncHelper.Timeout = Timeout;
        _syncHelper.SetPresence = SetPresence;
        _syncHelper.Filter = Filter;
        _syncHelper.FullState = FullState;
        // run sync
        var sync = await _syncHelper.SyncAsync(cancellationToken);
        if (sync is null) return await ContinueAsync(cancellationToken);
        if (MergedState is null) MergedState = sync;
        else MergedState = MergeSyncs(MergedState, sync);
        Since = sync.NextBatch;
        return (sync, MergedState);
    }

    private SyncResponse MergeSyncs(SyncResponse oldState, SyncResponse newState) {
        oldState.NextBatch = newState.NextBatch ?? oldState.NextBatch;

        oldState.AccountData ??= new EventList();
        oldState.AccountData.Events ??= [];
        if (newState.AccountData?.Events is not null)
            oldState.AccountData.Events.MergeStateEventLists(newState.AccountData?.Events ?? new List<StateEventResponse>());

        oldState.Presence ??= new SyncResponse.PresenceDataStructure();
        if (newState.Presence?.Events is not null)
            oldState.Presence.Events.MergeStateEventLists(newState.Presence?.Events ?? new List<StateEventResponse>());

        oldState.DeviceOneTimeKeysCount ??= new Dictionary<string, int>();
        if (newState.DeviceOneTimeKeysCount is not null)
            foreach (var (key, value) in newState.DeviceOneTimeKeysCount)
                oldState.DeviceOneTimeKeysCount[key] = value;

        oldState.Rooms ??= new SyncResponse.RoomsDataStructure();
        if (newState.Rooms is not null)
            oldState.Rooms = MergeRoomsDataStructure(oldState.Rooms, newState.Rooms);

        oldState.ToDevice ??= new EventList();
        oldState.ToDevice.Events ??= [];
        if (newState.ToDevice?.Events is not null)
            oldState.ToDevice.Events.MergeStateEventLists(newState.ToDevice?.Events ?? new List<StateEventResponse>());

        oldState.DeviceLists ??= new SyncResponse.DeviceListsDataStructure();
        oldState.DeviceLists.Changed ??= [];
        oldState.DeviceLists.Left ??= [];
        if (newState.DeviceLists?.Changed is not null)
            foreach (var s in newState.DeviceLists.Changed!)
                oldState.DeviceLists.Changed.Add(s);
        if (newState.DeviceLists?.Left is not null)
            foreach (var s in newState.DeviceLists.Left!)
                oldState.DeviceLists.Left.Add(s);

        return oldState;
    }

#region Merge rooms

    private SyncResponse.RoomsDataStructure MergeRoomsDataStructure(SyncResponse.RoomsDataStructure oldState, SyncResponse.RoomsDataStructure newState) {
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

    private SyncResponse.RoomsDataStructure.LeftRoomDataStructure MergeLeftRoomDataStructure(SyncResponse.RoomsDataStructure.LeftRoomDataStructure oldData,
        SyncResponse.RoomsDataStructure.LeftRoomDataStructure newData) {
        oldData.AccountData ??= new EventList();
        oldData.AccountData.Events ??= new List<StateEventResponse>();
        oldData.Timeline ??= new SyncResponse.RoomsDataStructure.JoinedRoomDataStructure.TimelineDataStructure();
        oldData.Timeline.Events ??= new List<StateEventResponse>();
        oldData.State ??= new EventList();
        oldData.State.Events ??= new List<StateEventResponse>();

        if (newData.AccountData?.Events is not null)
            oldData.AccountData.Events.MergeStateEventLists(newData.AccountData?.Events ?? new List<StateEventResponse>());

        if (newData.Timeline?.Events is not null)
            oldData.Timeline.Events.MergeStateEventLists(newData.Timeline?.Events ?? new List<StateEventResponse>());
        oldData.Timeline.Limited = newData.Timeline?.Limited ?? oldData.Timeline.Limited;
        oldData.Timeline.PrevBatch = newData.Timeline?.PrevBatch ?? oldData.Timeline.PrevBatch;

        if (newData.State?.Events is not null)
            oldData.State.Events.MergeStateEventLists(newData.State?.Events ?? new List<StateEventResponse>());

        return oldData;
    }

    private SyncResponse.RoomsDataStructure.InvitedRoomDataStructure MergeInvitedRoomDataStructure(SyncResponse.RoomsDataStructure.InvitedRoomDataStructure oldData,
        SyncResponse.RoomsDataStructure.InvitedRoomDataStructure newData) {
        oldData.InviteState ??= new EventList();
        oldData.InviteState.Events ??= new List<StateEventResponse>();
        if (newData.InviteState?.Events is not null)
            oldData.InviteState.Events.MergeStateEventLists(newData.InviteState?.Events ?? new List<StateEventResponse>());

        return oldData;
    }

    private SyncResponse.RoomsDataStructure.JoinedRoomDataStructure MergeJoinedRoomDataStructure(SyncResponse.RoomsDataStructure.JoinedRoomDataStructure oldData,
        SyncResponse.RoomsDataStructure.JoinedRoomDataStructure newData) {
        oldData.AccountData ??= new EventList();
        oldData.AccountData.Events ??= new List<StateEventResponse>();
        oldData.Timeline ??= new SyncResponse.RoomsDataStructure.JoinedRoomDataStructure.TimelineDataStructure();
        oldData.Timeline.Events ??= new List<StateEventResponse>();
        oldData.State ??= new EventList();
        oldData.State.Events ??= new List<StateEventResponse>();
        oldData.Ephemeral ??= new EventList();
        oldData.Ephemeral.Events ??= new List<StateEventResponse>();

        if (newData.AccountData?.Events is not null)
            oldData.AccountData.Events.MergeStateEventLists(newData.AccountData?.Events ?? new List<StateEventResponse>());

        if (newData.Timeline?.Events is not null)
            oldData.Timeline.Events.MergeStateEventLists(newData.Timeline?.Events ?? new List<StateEventResponse>());
        oldData.Timeline.Limited = newData.Timeline?.Limited ?? oldData.Timeline.Limited;
        oldData.Timeline.PrevBatch = newData.Timeline?.PrevBatch ?? oldData.Timeline.PrevBatch;

        if (newData.State?.Events is not null)
            oldData.State.Events.MergeStateEventLists(newData.State?.Events ?? new List<StateEventResponse>());

        if (newData.Ephemeral?.Events is not null)
            oldData.Ephemeral.Events.MergeStateEventLists(newData.Ephemeral?.Events ?? new List<StateEventResponse>());

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