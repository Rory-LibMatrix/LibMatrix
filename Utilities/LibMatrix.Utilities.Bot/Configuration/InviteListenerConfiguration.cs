using System.Diagnostics.CodeAnalysis;
using LibMatrix.Filters;

namespace LibMatrix.Utilities.Bot.Configuration;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global", Justification = "Configuration")]
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global", Justification = "Configuration")]
public class InviteListenerSyncConfiguration {
    public SyncFilter? Filter { get; set; }
    public TimeSpan? MinimumSyncTime { get; set; }
    public int? Timeout { get; set; }
    public string? Presence { get; set; }
    public bool InitialSyncOnStartup { get; set; }
}

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global", Justification = "Configuration")]
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global", Justification = "Configuration")]
public class InviteListenerConfiguration {
    public InviteListenerSyncConfiguration SyncConfiguration { get; set; } = new();
}