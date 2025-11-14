namespace LibMatrix.HomeserverEmulator.Services;

public class PaginationTokenResolverService(ILogger<PaginationTokenResolverService> logger, RoomStore roomStore, UserStore userStore) {
    public async Task<long?> ResolveTokenToTimestamp(string token) {
        logger.LogTrace("ResolveTokenToTimestamp({token})", token);
        if (token.StartsWith('$')) {
            //we have an event ID
            foreach (var room in roomStore._rooms) {
                var evt = await ResolveTokenToEvent(token, room);
                if (evt is not null) return evt.OriginServerTs;
            }

            // event not found
            throw new NotImplementedException();
        }
        else {
            // we have a sync token 
            foreach (var user in userStore._users) {
                foreach (var (_, session) in user.AccessTokens) {
                    if (!session.SyncStates.TryGetValue(token, out var syncState)) continue;
                    long? maxTs = 0;
                    foreach (var room in syncState.RoomPositions) {
                        var roomObj = roomStore.GetRoomById(room.Key);
                        if (roomObj is null)
                            continue;
                        var ts = roomObj.Timeline.Last().OriginServerTs;
                        if (ts > maxTs) maxTs = ts;
                    }

                    return maxTs;
                }
            }

            throw new NotImplementedException();
        }
    }

    public Task<MatrixEventResponse?> ResolveTokenToEvent(string token, RoomStore.Room room) {
        if (token.StartsWith('$')) {
            //we have an event ID
            logger.LogTrace("ResolveTokenToEvent(EventId({token}), Room({room})): searching for event...", token, room.RoomId);

            var evt = room.Timeline.SingleOrDefault(x => x.EventId == token);
            if (evt is not null) return Task.FromResult(evt);
            logger.LogTrace("ResolveTokenToEvent({token}, Room({room})): event not in requested room...", token, room.RoomId);
            return Task.FromResult<MatrixEventResponse?>(null);
        }
        else {
            // we have a sync token
            logger.LogTrace("ResolveTokenToEvent(SyncToken({token}), Room({room}))", token, room.RoomId);
            throw new NotImplementedException();
        }
    }
}