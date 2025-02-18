using System.Text.Json.Serialization;

namespace LibMatrix.Utilities.Bot.AppServices;

public class AppServiceConfiguration {
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("sender_localpart")]
    public string SenderLocalpart { get; set; }

    [JsonPropertyName("as_token")]
    public string AppserviceToken { get; set; }

    [JsonPropertyName("hs_token")]
    public string HomeserverToken { get; set; }

    [JsonPropertyName("protocols")]
    public List<string>? Protocols { get; set; }

    [JsonPropertyName("rate_limited")]
    public bool? RateLimited { get; set; }

    [JsonPropertyName("namespaces")]
    public AppserviceNamespaces Namespaces { get; set; }

    public class AppserviceNamespaces {
        [JsonPropertyName("users")]
        public List<AppserviceNamespace>? Users { get; set; } = null;

        [JsonPropertyName("aliases")]
        public List<AppserviceNamespace>? Aliases { get; set; } = null;

        [JsonPropertyName("rooms")]
        public List<AppserviceNamespace>? Rooms { get; set; } = null;

        public class AppserviceNamespace {
            [JsonPropertyName("exclusive")]
            public bool Exclusive { get; set; }

            [JsonPropertyName("regex")]
            public string Regex { get; set; }
        }
    }

    /// <summary>
    /// Please dont look at code, it's horrifying but works
    /// </summary>
    /// <returns></returns>
    public string ToYaml() {
        var yaml = $"""
                    id: "{Id ?? throw new NullReferenceException("Id is null")}"
                    url: {(Url is null ? "null" : $"\"{Url}\"")}
                    as_token: "{AppserviceToken ?? throw new NullReferenceException("AppserviceToken is null")}"
                    hs_token: "{HomeserverToken ?? throw new NullReferenceException("HomeserverToken is null")}"
                    sender_localpart: "{SenderLocalpart ?? throw new NullReferenceException("SenderLocalpart is null")}"

                    """;

        if (Protocols is not null && Protocols.Count > 0)
            yaml += $"""
                     protocols:
                        - "{Protocols[0] ?? throw new NullReferenceException("Protocols[0] is null")}"
                     """;
        else
            yaml += "protocols: []";
        yaml += "\n";
        if (RateLimited.HasValue)
            yaml += $"rate_limited: {RateLimited.Value.ToString().ToLower()}\n";
        else
            yaml += "rate_limited: false\n";

        yaml += "namespaces: \n";

        if (Namespaces.Users is null || Namespaces.Users.Count == 0)
            yaml += "  users: []";
        else
            Namespaces.Users.ForEach(x =>
                yaml += $"""
                             users:
                                 - exclusive: {x.Exclusive.ToString().ToLower()}
                                   regex: "{x.Regex ?? throw new NullReferenceException("x.Regex is null")}"
                         """);
        yaml += "\n";

        if (Namespaces.Aliases is null || Namespaces.Aliases.Count == 0)
            yaml += "  aliases: []";
        else
            Namespaces.Aliases.ForEach(x =>
                yaml += $"""
                             aliases:
                                 - exclusive: {x.Exclusive.ToString().ToLower()}
                                   regex: "{x.Regex ?? throw new NullReferenceException("x.Regex is null")}"
                         """);
        yaml += "\n";
        if (Namespaces.Rooms is null || Namespaces.Rooms.Count == 0)
            yaml += "  rooms: []";
        else
            Namespaces.Rooms.ForEach(x =>
                yaml += $"""
                             rooms:
                                 - exclusive: {x.Exclusive.ToString().ToLower()}
                                   regex: "{x.Regex ?? throw new NullReferenceException("x.Regex is null")}"
                         """);

        return yaml;
    }
}