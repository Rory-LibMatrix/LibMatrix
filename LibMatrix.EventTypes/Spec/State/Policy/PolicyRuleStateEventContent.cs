using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
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

[DebuggerDisplay("""{GetType().Name.Replace("PolicyRuleEventContent", ""),nq} policy matching {Entity}, Reason: {Reason}""")]
public abstract class PolicyRuleEventContent : EventContent {
    // public PolicyRuleEventContent() => Console.WriteLine($"init policy {GetType().Name}");

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
    public string? Reason { get; set; }

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

    [JsonPropertyName("org.matrix.msc4205.hashes")]
    [TableHide]
    public PolicyHash? Hashes { get; set; }

    public string GetDraupnir2StateKey() => Convert.ToBase64String(SHA256.HashData($"{Entity}{Recommendation}".AsBytes().ToArray()));
    public Regex? GetEntityRegex() => Entity is null ? null : new(Entity.Replace(".", "\\.").Replace("*", ".*").Replace("?", "."), RegexOptions.Compiled);
    public bool IsGlobRule() => !string.IsNullOrWhiteSpace(Entity) && (Entity.Contains('*') || Entity.Contains('?'));
    public bool IsHashedRule() => string.IsNullOrWhiteSpace(Entity) && Hashes is not null;

    public bool EntityMatches(string entity) {
        if (string.IsNullOrWhiteSpace(entity)) return false;

        if (!string.IsNullOrWhiteSpace(Entity)) {
            // Check if entity is equal regardless of glob check
            var match = Entity == entity || (IsGlobRule() && GetEntityRegex()!.IsMatch(entity));
            if (match) return match;
        }

        if (Hashes is not null) {
            if (!string.IsNullOrWhiteSpace(Hashes.Sha256)) {
                var hash = SHA256.HashData(entity.AsBytes().ToArray());
                var match = Convert.ToBase64String(hash) == Hashes.Sha256;
                if (match) return match;
            }
        }

        return false;
    }

    public string? GetNormalizedRecommendation() {
        if (Recommendation is "m.ban" or "org.matrix.mjolnir.ban")
            return PolicyRecommendationTypes.Ban;

        if (Recommendation is "m.takedown" or "org.matrix.msc4204.takedown")
            return "m.takedown";

        return Recommendation;
    }

    public string? GetSpecRecommendation() {
        if (Recommendation is "m.ban" or "org.matrix.mjolnir.ban")
            return PolicyRecommendationTypes.Ban;

        if (Recommendation is "m.mute" or "support.feline.policy.recommendation_mute")
            return PolicyRecommendationTypes.Mute;

        if (Recommendation is "m.takedown" or "org.matrix.msc4204.takedown")
            return PolicyRecommendationTypes.Takedown;

        return Recommendation;
    }
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

    /// <summary>
    ///     Take down the user with all means available
    /// </summary>
    public static string Takedown = "org.matrix.msc4204.takedown"; //stable prefix: m.takedown, msc pending
}

public class PolicyHash {
    [JsonPropertyName("sha256")]
    public string? Sha256 { get; set; }
    
    [JsonExtensionData]
    public Dictionary<string, object>? AdditionalProperties { get; set; }
}

// public class PolicySchemaDefinition {
//     public required string Name { get; set; }
//     public required bool Optional { get; set; }
//     
// }