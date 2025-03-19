using System.Collections.Frozen;
using LibMatrix.EventTypes;
using LibMatrix.EventTypes.Spec.State.Policy;
using LibMatrix.Homeservers;

namespace LibMatrix.RoomTypes;

public class PolicyRoom(AuthenticatedHomeserverGeneric homeserver, string roomId) : GenericRoom(homeserver, roomId) {
    public const string TypeName = "support.feline.policy.lists.msc.v1";
    
    private static readonly FrozenSet<string> UserPolicyEventTypes = EventContent.GetMatchingEventTypes<UserPolicyRuleEventContent>().ToFrozenSet();
    private static readonly FrozenSet<string> ServerPolicyEventTypes = EventContent.GetMatchingEventTypes<ServerPolicyRuleEventContent>().ToFrozenSet();
    private static readonly FrozenSet<string> RoomPolicyEventTypes = EventContent.GetMatchingEventTypes<RoomPolicyRuleEventContent>().ToFrozenSet();
    private static readonly FrozenSet<string> SpecPolicyEventTypes = [..UserPolicyEventTypes, ..ServerPolicyEventTypes, ..RoomPolicyEventTypes];

    public async IAsyncEnumerable<StateEventResponse> GetPoliciesAsync() {
        var fullRoomState = GetFullStateAsync();
        await foreach (var eventResponse in fullRoomState) {
            if (SpecPolicyEventTypes.Contains(eventResponse!.Type)) {
                yield return eventResponse;
            }
        }
    }

    public async IAsyncEnumerable<StateEventResponse> GetUserPoliciesAsync() {
        var fullRoomState = GetPoliciesAsync();
        await foreach (var eventResponse in fullRoomState) {
            if (UserPolicyEventTypes.Contains(eventResponse!.Type)) {
                yield return eventResponse;
            }
        }
    }

    public async IAsyncEnumerable<StateEventResponse> GetServerPoliciesAsync() {
        var fullRoomState = GetPoliciesAsync();
        await foreach (var eventResponse in fullRoomState) {
            if (ServerPolicyEventTypes.Contains(eventResponse!.Type)) {
                yield return eventResponse;
            }
        }
    }

    public async IAsyncEnumerable<StateEventResponse> GetRoomPoliciesAsync() {
        var fullRoomState = GetPoliciesAsync();
        await foreach (var eventResponse in fullRoomState) {
            if (RoomPolicyEventTypes.Contains(eventResponse!.Type)) {
                yield return eventResponse;
            }
        }
    }
}