using System.Diagnostics.CodeAnalysis;
using LibMatrix.Filters;

namespace LibMatrix.Utilities.Bot.Configuration;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global", Justification = "Configuration")]
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global", Justification = "Configuration")]
public class CommandListenerSyncConfiguration {
    // public SyncFilter? Filter { get; set; }
    public TimeSpan? MinimumSyncTime { get; set; }
    public int? Timeout { get; set; }
    public string? Presence { get; set; }
    // public bool InitialSyncOnStartup { get; set; }
}

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global", Justification = "Configuration")]
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global", Justification = "Configuration")]
public class CommandListenerConfiguration {
    public CommandListenerSyncConfiguration SyncConfiguration { get; set; } = new();

    public required List<string> Prefixes { get; set; }
    public bool MentionPrefix { get; set; }
    public bool SelfCommandsOnly { get; set; }
}