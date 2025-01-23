using Microsoft.Extensions.Configuration;

namespace LibMatrix.Utilities.Bot;

public class LibMatrixBotConfiguration {
    public LibMatrixBotConfiguration(IConfiguration config) => config.GetRequiredSection("LibMatrixBot").Bind(this);
    public string Homeserver { get; set; }
    public string? AccessToken { get; set; }
    public string? AccessTokenPath { get; set; }
    public List<string> Prefixes { get; set; }
    public bool MentionPrefix { get; set; }
    public string? LogRoom { get; set; }
}