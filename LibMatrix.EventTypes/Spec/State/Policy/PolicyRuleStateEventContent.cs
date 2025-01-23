using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ArcaneLibs.Attributes;
using ArcaneLibs.Extensions;

namespace LibMatrix.EventTypes.Spec.State.Policy;

//spec
[MatrixEvent(EventName = EventId)]                                         //spec
[MatrixEvent(EventName = "m.room.rule.server", Legacy = true)]             //???
[MatrixEvent(EventName = "org.matrix.mjolnir.rule.server", Legacy = true)] //legacy
[FriendlyName(Name = "Server policy", NamePlural = "Server policies")]
public class ServerPolicyRuleEventContent : PolicyRuleEventContent {
    public const string EventId = "m.policy.rule.server";
}

[MatrixEvent(EventName = EventId)]                                       //spec
[MatrixEvent(EventName = "m.room.rule.user", Legacy = true)]             //???
[MatrixEvent(EventName = "org.matrix.mjolnir.rule.user", Legacy = true)] //legacy
[FriendlyName(Name = "User policy", NamePlural = "User policies")]
public class UserPolicyRuleEventContent : PolicyRuleEventContent {
    public const string EventId = "m.policy.rule.user";
}

[MatrixEvent(EventName = EventId)]                                       //spec
[MatrixEvent(EventName = "m.room.rule.room", Legacy = true)]             //???
[MatrixEvent(EventName = "org.matrix.mjolnir.rule.room", Legacy = true)] //legacy
[FriendlyName(Name = "Room policy", NamePlural = "Room policies")]
public class RoomPolicyRuleEventContent : PolicyRuleEventContent {
    public const string EventId = "m.policy.rule.room";
}

public abstract class PolicyRuleEventContent : EventContent {
    public PolicyRuleEventContent() => Console.WriteLine($"init policy {GetType().Name}");
    private string? _reason;

    /// <summary>
    ///     Entity this ban applies to, can use * and ? as globs.
    ///     Policy is invalid if entity is null
    /// </summary>
    [JsonPropertyName("entity")]
    [FriendlyName(Name = "Entity")]
    public string? Entity { get; set; }

    // private bool init;

    /// <summary>
    ///     Reason this user is banned
    /// </summary>
    [JsonPropertyName("reason")]
    [FriendlyName(Name = "Reason")]
    public virtual string? Reason {
        get =>
            // Console.WriteLine($"Read policy reason: {_reason}");
            _reason;
        set =>
            // Console.WriteLine($"Set policy reason: {value}");
            // if(init)
            // Console.WriteLine(string.Join('\n', Environment.StackTrace.Split('\n')[..5]));
            // init = true;
            _reason = value;
    }

    /// <summary>
    ///     Suggested action to take
    /// </summary>
    [JsonPropertyName("recommendation")]
    [FriendlyName(Name = "Recommendation")]
    public string? Recommendation { get; set; }

    /// <summary>
    ///     Expiry time in milliseconds since the unix epoch, or null if the ban has no expiry.
    /// </summary>
    [JsonPropertyName("support.feline.policy.expiry.rev.2")] //stable prefix: expiry, msc pending
    [TableHide]
    public long? Expiry { get; set; }

    //utils
    /// <summary>
    ///     Readable expiry time, provided for easy interaction
    /// </summary>
    [JsonPropertyName("gay.rory.matrix_room_utils.readable_expiry_time_utc")]
    [FriendlyName(Name = "Expires at")]
    [TableHide]
    public DateTime? ExpiryDateTime {
        get => Expiry == null ? null : DateTimeOffset.FromUnixTimeMilliseconds(Expiry.Value).DateTime;
        set {
            if (value is not null)
                Expiry = ((DateTimeOffset)value).ToUnixTimeMilliseconds();
        }
    }

    public string GetDraupnir2StateKey() => Convert.ToBase64String(SHA256.HashData($"{Entity}{Recommendation}".AsBytes().ToArray()));

    public Regex? GetEntityRegex() => Entity is null ? null : new(Entity.Replace(".", "\\.").Replace("*", ".*").Replace("?", "."));

    public bool EntityMatches(string entity) =>
        Entity != null
        && (
            Entity == entity
            || (
                Entity.Contains("*") || Entity.Contains("?")
                    ? GetEntityRegex()!.IsMatch(entity)
                    : entity == Entity
            )
        );
}

public static class PolicyRecommendationTypes {
    /// <summary>
    ///     Ban this user
    /// </summary>
    public static string Ban = "m.ban";

    /// <summary>
    ///     Mute this user
    /// </summary>
    public static string Mute = "support.feline.policy.recommendation_mute"; //stable prefix: m.mute, msc pending
}

// public class PolicySchemaDefinition {
//     public required string Name { get; set; }
//     public required bool Optional { get; set; }
//     
// }