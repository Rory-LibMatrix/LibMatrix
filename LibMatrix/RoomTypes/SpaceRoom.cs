using ArcaneLibs.Extensions;
using LibMatrix.Homeservers;
using Microsoft.Extensions.Logging;

namespace LibMatrix.RoomTypes;

public class SpaceRoom(AuthenticatedHomeserverGeneric homeserver, string roomId) : GenericRoom(homeserver, roomId) {
    private readonly GenericRoom _room;

    public async IAsyncEnumerable<GenericRoom> GetChildrenAsync(bool includeRemoved = false) {
        var rooms = new List<GenericRoom>();
        var state = GetFullStateAsync();
        await foreach (var stateEvent in state) {
            if (stateEvent!.Type != "m.space.child") continue;
            if (stateEvent.RawContent!.ToJson() != "{}" || includeRemoved)
                yield return Homeserver.GetRoom(stateEvent.StateKey);
        }
    }

    public async Task<EventIdResponse> AddChildAsync(GenericRoom room) {
        var members = room.GetMembersAsync(true);
        Dictionary<string, int> memberCountByHs = new();
        await foreach (var member in members) {
            var server = member.StateKey.Split(':')[1];
            if (memberCountByHs.ContainsKey(server)) memberCountByHs[server]++;
            else memberCountByHs[server] = 1;
        }

        var resp = await SendStateEventAsync("m.space.child", room.RoomId, new {
            via = memberCountByHs
                .OrderByDescending(x => x.Value)
                .Select(x => x.Key)
                .Take(10)
        });
        return resp;
    }
}
