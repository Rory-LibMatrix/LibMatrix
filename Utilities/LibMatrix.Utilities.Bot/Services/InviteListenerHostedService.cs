using LibMatrix.Filters;
using LibMatrix.Helpers;
using LibMatrix.Homeservers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LibMatrix.Utilities.Bot.Services;

public class InviteHandlerHostedService : IHostedService {
    private readonly AuthenticatedHomeserverGeneric _hs;
    private readonly ILogger<InviteHandlerHostedService> _logger;
    private readonly Func<InviteEventArgs, Task> _inviteHandler;

    private Task? _listenerTask;

    public InviteHandlerHostedService(AuthenticatedHomeserverGeneric hs, ILogger<InviteHandlerHostedService> logger,
        Func<InviteEventArgs, Task> inviteHandler) {
        logger.LogInformation("{} instantiated!", GetType().Name);
        _hs = hs;
        _logger = logger;
        _inviteHandler = inviteHandler;
    }

    /// <summary>Triggered when the application host is ready to start the service.</summary>
    /// <param name="cancellationToken">Indicates that the start process has been aborted.</param>
    public Task StartAsync(CancellationToken cancellationToken) {
        _listenerTask = Run(cancellationToken);
        _logger.LogInformation("Command listener started (StartAsync)!");
        return Task.CompletedTask;
    }

    private async Task? Run(CancellationToken cancellationToken) {
        _logger.LogInformation("Starting invite listener!");
        var filter = await _hs.NamedCaches.FilterCache.GetOrSetValueAsync("gay.rory.libmatrix.utilities.bot.command_listener_syncfilter.dev2", new SyncFilter() {
            AccountData = new SyncFilter.EventFilter(notTypes: ["*"], limit: 1),
            Presence = new SyncFilter.EventFilter(notTypes: ["*"]),
            Room = new SyncFilter.RoomFilter() {
                AccountData = new SyncFilter.RoomFilter.StateFilter(notTypes: ["*"]),
                Ephemeral = new SyncFilter.RoomFilter.StateFilter(notTypes: ["*"]),
                State = new SyncFilter.RoomFilter.StateFilter(notTypes: ["*"]),
                Timeline = new SyncFilter.RoomFilter.StateFilter(types: ["m.room.message"], notSenders: [_hs.WhoAmI.UserId]),
            }
        });

        var syncHelper = new SyncHelper(_hs, _logger) {
            Timeout = 300_000,
            FilterId = filter
        };
        syncHelper.InviteReceivedHandlers.Add(async invite => {
            _logger.LogInformation("Received invite to room {}", invite.Key);

            var inviteEventArgs = new InviteEventArgs() {
                RoomId = invite.Key,
                MemberEvent = invite.Value.InviteState.Events.First(x => x.Type == "m.room.member" && x.StateKey == _hs.WhoAmI.UserId),
                Homeserver = _hs
            };
            await _inviteHandler(inviteEventArgs);
        });

        await syncHelper.RunSyncLoopAsync(cancellationToken: cancellationToken);
    }

    /// <summary>Triggered when the application host is performing a graceful shutdown.</summary>
    /// <param name="cancellationToken">Indicates that the shutdown process should no longer be graceful.</param>
    public async Task StopAsync(CancellationToken cancellationToken) {
        _logger.LogInformation("Shutting down invite listener!");
        if (_listenerTask is null) {
            _logger.LogError("Could not shut down invite listener task because it was null!");
            return;
        }

        await _listenerTask.WaitAsync(cancellationToken);
    }

    public class InviteEventArgs {
        public string RoomId { get; set; }
        public StateEventResponse MemberEvent { get; set; }
        public AuthenticatedHomeserverGeneric Homeserver { get; set; }
    }
}