using System.Collections.Frozen;
using LibMatrix.EventTypes.Common;
using LibMatrix.EventTypes.Spec.State.RoomInfo;
using LibMatrix.EventTypes.Spec.State.Space;
using LibMatrix.Filters;

namespace LibMatrix.Utilities;

public static class CommonSyncFilters {
    public const string GetAccountData = "gay.rory.libmatrix.get_account_data.v0";
    public const string GetAccountDataWithRooms = "gay.rory.libmatrix.get_account_data_with_rooms.v0";
    public const string GetBasicRoomInfo = "gay.rory.matrixutils.get_basic_room_info.v0";
    public const string GetSpaceRelations = "gay.rory.matrixutils.get_space_relations.v0";
    public const string GetOwnMemberEvents = "gay.rory.matrixutils.get_own_member_events.v0";

    public static SyncFilter GetAccountDataFilter => new() {
        Presence = new SyncFilter.EventFilter(notTypes: ["*"]),
        Room = new SyncFilter.RoomFilter() {
            Rooms = []
        }
    };

    public static SyncFilter GetAccountDataWithRoomsFilter => new() {
        Presence = new SyncFilter.EventFilter(notTypes: ["*"]),
        Room = new SyncFilter.RoomFilter() {
            State = new SyncFilter.RoomFilter.StateFilter(notTypes: ["*"]),
            Ephemeral = new SyncFilter.RoomFilter.StateFilter(notTypes: ["*"]),
            Timeline = new SyncFilter.RoomFilter.StateFilter(notTypes: ["*"])
        }
    };

    public static SyncFilter GetBasicRoomDataFilter => new() {
        AccountData = new SyncFilter.EventFilter(notTypes: ["*"], limit: 1),
        Presence = new SyncFilter.EventFilter(notTypes: ["*"], limit: 1),
        Room = new SyncFilter.RoomFilter {
            AccountData = new SyncFilter.RoomFilter.StateFilter(rooms: []),
            Ephemeral = new SyncFilter.RoomFilter.StateFilter(rooms: []),
            State = new SyncFilter.RoomFilter.StateFilter {
                Types = new List<string> {
                    RoomCreateEventContent.EventId,
                    RoomNameEventContent.EventId,
                    RoomAvatarEventContent.EventId,
                    MjolnirShortcodeEventContent.EventId,
                    RoomPowerLevelEventContent.EventId
                },
                LazyLoadMembers = true, IncludeRedundantMembers = false
            },
            Timeline = new SyncFilter.RoomFilter.StateFilter(rooms: [])
        }
    };

    public static SyncFilter GetSpaceRelationsFilter => new() {
        AccountData = new SyncFilter.EventFilter(notTypes: ["*"], limit: 1),
        Presence = new SyncFilter.EventFilter(notTypes: ["*"], limit: 1),
        Room = new SyncFilter.RoomFilter {
            AccountData = new SyncFilter.RoomFilter.StateFilter(rooms: []),
            Ephemeral = new SyncFilter.RoomFilter.StateFilter(rooms: []),
            State = new SyncFilter.RoomFilter.StateFilter {
                Types = new List<string> {
                    SpaceChildEventContent.EventId,
                    SpaceParentEventContent.EventId
                },
                LazyLoadMembers = true, IncludeRedundantMembers = false
            },
            Timeline = new SyncFilter.RoomFilter.StateFilter(rooms: [])
        }
    };

    public static SyncFilter GetOwnMemberEventsFilter => new() {
        AccountData = new SyncFilter.EventFilter(types: ["m.room.member"], limit: 1),
        Presence = new SyncFilter.EventFilter(notTypes: ["*"], limit: 1),
        Room = new SyncFilter.RoomFilter {
            AccountData = new SyncFilter.RoomFilter.StateFilter(rooms: []),
            Ephemeral = new SyncFilter.RoomFilter.StateFilter(rooms: []),
            State = new SyncFilter.RoomFilter.StateFilter {
                Types = new List<string> {
                    RoomMemberEventContent.EventId
                },
                LazyLoadMembers = true, IncludeRedundantMembers = false,
                Senders = ["@me"]
            },
            Timeline = new SyncFilter.RoomFilter.StateFilter(rooms: [])
        }
    };

    // This must be down here, due to statics load order
    public static FrozenDictionary<string, SyncFilter> FilterMap => new Dictionary<string, SyncFilter>() {
        [GetAccountData] = GetAccountDataFilter,
        [GetAccountDataWithRooms] = GetAccountDataWithRoomsFilter,
        [GetBasicRoomInfo] = GetBasicRoomDataFilter,
        [GetSpaceRelations] = GetSpaceRelationsFilter,
        [GetOwnMemberEvents] = GetOwnMemberEventsFilter
    }.ToFrozenDictionary();
}