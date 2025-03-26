using System.Diagnostics.CodeAnalysis;
using LibMatrix.Filters;
using LibMatrix.Helpers;
using LibMatrix.Homeservers;
using LibMatrix.Utilities.Bot.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LibMatrix.Utilities.Bot.Services;

public class InviteHandlerHostedService(
    ILogger<InviteHandlerHostedService> logger,
    AuthenticatedHomeserverGeneric hs,
    InviteHandlerHostedService.InviteListenerSyncConfiguration listenerSyncConfiguration,
    Func<RoomInviteContext, Task> inviteHandler
) : IHostedService {
    private Task? _listenerTask;
    private CancellationTokenSource _cts = new();

    private readonly SyncHelper _syncHelper = new(hs, logger) {
        Timeout = listenerSyncConfiguration.Timeout ?? 30_000,
        MinimumDelay = listenerSyncConfiguration.MinimumSyncTime ?? new(0),
        SetPresence = listenerSyncConfiguration.Presence ?? "online"
    };

    /// <summary>Triggered when the application host is ready to start the service.</summary>
    /// <param name="cancellationToken">Indicates that the start process has been aborted.</param>
    public Task StartAsync(CancellationToken cancellationToken) {
        _listenerTask = Run(cancellationToken);
        return Task.CompletedTask;
    }

    private async Task? Run(CancellationToken cancellationToken) {
        logger.LogInformation("Starting invite listener!");
        var nextBatchFile = $"inviteHandler.{hs.WhoAmI.UserId}.{hs.WhoAmI.DeviceId}.nextBatch";
        if (listenerSyncConfiguration.Filter is not null) {
            _syncHelper.Filter = listenerSyncConfiguration.Filter;
        }
        else {
            _syncHelper.FilterId = await hs.NamedCaches.FilterCache.GetOrSetValueAsync("gay.rory.libmatrix.utilities.bot.invite_listener_syncfilter.dev0", new SyncFilter() {
                AccountData = SyncFilter.EventFilter.Empty,
                Presence = SyncFilter.EventFilter.Empty,
                Room = new SyncFilter.RoomFilter {
                    AccountData = SyncFilter.RoomFilter.StateFilter.Empty,
                    Ephemeral = SyncFilter.RoomFilter.StateFilter.Empty,
                    State = SyncFilter.RoomFilter.StateFilter.Empty,
                    Timeline = new SyncFilter.RoomFilter.StateFilter(types: [], notSenders: [hs.WhoAmI.UserId]),
                    IncludeLeave = false
                }
            });
        }

        if (File.Exists(nextBatchFile) && !listenerSyncConfiguration.InitialSyncOnStartup) {
            _syncHelper.Since = await File.ReadAllTextAsync(nextBatchFile, cancellationToken);
        }

        _syncHelper.InviteReceivedHandlers.Add(async invite => {
            logger.LogInformation("Received invite to room {}", invite.Key);
            var inviteEventArgs = new RoomInviteContext() {
                RoomId = invite.Key,
                InviteData = invite.Value,
                MemberEvent = invite.Value.InviteState?.Events?.First(x => x.Type == "m.room.member" && x.StateKey == hs.WhoAmI.UserId)
                              ?? throw new LibMatrixException() {
                                  ErrorCode = LibMatrixException.ErrorCodes.M_NOT_FOUND,
                                  Error = "Room invite doesn't contain a membership event!"
                              },
                Homeserver = hs
            };
            await inviteHandler(inviteEventArgs);
        });

        if (!listenerSyncConfiguration.InitialSyncOnStartup)
            _syncHelper.SyncReceivedHandlers.Add(sync => File.WriteAllTextAsync(nextBatchFile, sync.NextBatch, cancellationToken));
        await _syncHelper.RunSyncLoopAsync(cancellationToken: _cts.Token);
    }

    /// <summary>Triggered when the application host is performing a graceful shutdown.</summary>
    /// <param name="cancellationToken">Indicates that the shutdown process should no longer be graceful.</param>
    public async Task StopAsync(CancellationToken cancellationToken) {
        logger.LogInformation("Shutting down invite listener!");
        if (_listenerTask is null) {
            logger.LogError("Could not shut down invite listener task because it was null!");
            return;
        }

        await _cts.CancelAsync();
    }



    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global", Justification = "Configuration")]
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Global", Justification = "Configuration")]
    public class InviteListenerSyncConfiguration {
        public InviteListenerSyncConfiguration(IConfiguration config) => config.GetSection("LibMatrixBot:InviteHandler:SyncConfiguration").Bind(this);
        public SyncFilter? Filter { get; set; }
        public TimeSpan? MinimumSyncTime { get; set; }
        public int? Timeout { get; set; }
        public string? Presence { get; set; }
        public bool InitialSyncOnStartup { get; set; }
    }
}