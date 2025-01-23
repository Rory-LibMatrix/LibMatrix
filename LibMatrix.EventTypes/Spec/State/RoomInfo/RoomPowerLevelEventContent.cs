using System.Text.Json.Serialization;

namespace LibMatrix.EventTypes.Spec.State.RoomInfo;

[MatrixEvent(EventName = EventId)]
[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
public class RoomPowerLevelEventContent : EventContent {
    public const string EventId = "m.room.power_levels";

    [JsonPropertyName("ban")]
    public long? Ban { get; set; } = 50;

    [JsonPropertyName("events_default")]
    public long? EventsDefault { get; set; } = 0;

    [JsonPropertyName("invite")]
    public long? Invite { get; set; } = 0;

    [JsonPropertyName("kick")]
    public long? Kick { get; set; } = 50;

    [JsonPropertyName("notifications")]
    public NotificationsPL? NotificationsPl { get; set; } // = null!;

    [JsonPropertyName("redact")]
    public long? Redact { get; set; } = 50;

    [JsonPropertyName("state_default")]
    public long? StateDefault { get; set; } = 50;

    [JsonPropertyName("events")]
    public Dictionary<string, long>? Events { get; set; } // = null!;

    [JsonPropertyName("users")]
    public Dictionary<string, long>? Users { get; set; } // = null!;

    [JsonPropertyName("users_default")]
    public long? UsersDefault { get; set; } = 0;

    [Obsolete("Historical was a key related to MSC2716, a spec change on backfill that was dropped!", true)]
    [JsonIgnore]
    [JsonPropertyName("historical")]
    public long Historical { get; set; } // = 50;

    public class NotificationsPL {
        [JsonPropertyName("room")]
        public long Room { get; set; } = 50;
    }

    public bool IsUserAdmin(string userId) {
        ArgumentNullException.ThrowIfNull(userId);
        return Users.TryGetValue(userId, out var level) && level >= Events.Max(x => x.Value);
    }

    public bool UserHasTimelinePermission(string userId, string eventType) {
        ArgumentNullException.ThrowIfNull(userId);
        return Users.TryGetValue(userId, out var level) && level >= Events.GetValueOrDefault(eventType, EventsDefault ?? 0);
    }

    public bool UserHasStatePermission(string userId, string eventType, bool log = false) {
        ArgumentNullException.ThrowIfNull(userId);
        var userLevel = GetUserPowerLevel(userId);
        var eventLevel = GetStateEventPowerLevel(eventType);

        if (log)
            Console.WriteLine($"{userId}={userLevel} >= {eventType}={eventLevel} = {userLevel >= eventLevel}");

        return userLevel >= eventLevel;
    }

    public long GetUserPowerLevel(string userId) {
        ArgumentNullException.ThrowIfNull(userId);
        if (Users is null) return UsersDefault ?? 0;
        return Users.TryGetValue(userId, out var level) ? level : UsersDefault ?? 0;
    }

    public long GetStateEventPowerLevel(string eventType) {
        ArgumentNullException.ThrowIfNull(eventType);
        if (Events is null) return StateDefault ?? 0;
        return Events.TryGetValue(eventType, out var level) ? level : StateDefault ?? 0;
    }

    public long GetTimelineEventPowerLevel(string eventType) {
        ArgumentNullException.ThrowIfNull(eventType);
        if (Events is null) return EventsDefault ?? 0;
        return Events.TryGetValue(eventType, out var level) ? level : EventsDefault ?? 0;
    }

    public void SetUserPowerLevel(string userId, long powerLevel) {
        ArgumentNullException.ThrowIfNull(userId);
        Users ??= new Dictionary<string, long>();
        Users[userId] = powerLevel;
    }
}