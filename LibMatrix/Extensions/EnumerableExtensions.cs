using System.Collections.Frozen;
using System.Collections.Immutable;

namespace LibMatrix.Extensions;

public static class EnumerableExtensions {
    public static void MergeStateEventLists(this IList<StateEvent> oldState, IList<StateEvent> newState) {
        // foreach (var stateEvent in newState) {
        //     var old = oldState.FirstOrDefault(x => x.Type == stateEvent.Type && x.StateKey == stateEvent.StateKey);
        //     if (old is null) {
        //         oldState.Add(stateEvent);
        //         continue;
        //     }
        //
        //     oldState.Remove(old);
        //     oldState.Add(stateEvent);
        // }

        foreach (var e in newState) {
            switch (FindIndex(e)) {
                case -1:
                    oldState.Add(e);
                    break;
                case var index:
                    oldState[index] = e;
                    break;
            }
        }

        int FindIndex(StateEvent needle) {
            for (int i = 0; i < oldState.Count; i++) {
                var old = oldState[i];
                if (old.Type == needle.Type && old.StateKey == needle.StateKey)
                    return i;
            }

            return -1;
        }
    }

    public static void MergeStateEventLists(this IList<StateEventResponse> oldState, IList<StateEventResponse> newState) {
        foreach (var e in newState) {
            switch (FindIndex(e)) {
                case -1:
                    oldState.Add(e);
                    break;
                case var index:
                    oldState[index] = e;
                    break;
            }
        }

        int FindIndex(StateEventResponse needle) {
            for (int i = 0; i < oldState.Count; i++) {
                var old = oldState[i];
                if (old.Type == needle.Type && old.StateKey == needle.StateKey)
                    return i;
            }

            return -1;
        }
    }

    public static void MergeStateEventLists(this List<StateEventResponse> oldState, List<StateEventResponse> newState) {
        foreach (var e in newState) {
            switch (FindIndex(e)) {
                case -1:
                    oldState.Add(e);
                    break;
                case var index:
                    oldState[index] = e;
                    break;
            }
        }

        int FindIndex(StateEventResponse needle) {
            for (int i = 0; i < oldState.Count; i++) {
                var old = oldState[i];
                if (old.Type == needle.Type && old.StateKey == needle.StateKey)
                    return i;
            }

            return -1;
        }
    }
}