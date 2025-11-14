using System.Diagnostics.CodeAnalysis;
using ArcaneLibs.Extensions;
using LibMatrix.EventTypes.Spec;
using LibMatrix.EventTypes.Spec.State.RoomInfo;
using LibMatrix.Helpers;
using LibMatrix.Homeservers;
using LibMatrix.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LibMatrix.ExampleBot.Bot;

public class DevTestBot : IHostedService {
    private readonly HomeserverProviderService _homeserverProviderService;
    private readonly ILogger<DevTestBot> _logger;
    private readonly DevTestBotConfiguration _configuration;

    public DevTestBot(HomeserverProviderService homeserverProviderService, ILogger<DevTestBot> logger,
        DevTestBotConfiguration configuration) {
        logger.LogInformation("{} instantiated!", GetType().Name);
        _homeserverProviderService = homeserverProviderService;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>Triggered when the application host is ready to start the service.</summary>
    /// <param name="cancellationToken">Indicates that the start process has been aborted.</param>
    [SuppressMessage("ReSharper", "FunctionNeverReturns")]
    public async Task StartAsync(CancellationToken cancellationToken) {
        // Directory.GetFiles("bot_data/cache").ToList().ForEach(File.Delete);
        AuthenticatedHomeserverGeneric hs;
        try {
            hs = await _homeserverProviderService.GetAuthenticatedWithToken(_configuration.Homeserver,
                _configuration.AccessToken);
        }
        catch (Exception e) {
            _logger.LogError("{}", e.Message);
            throw;
        }

        var msg = new MessageBuilder().WithRainbowString("Meanwhile, I'm sitting here, still struggling with trying to rainbow. ^^'").Build();
        var res = await hs.ResolveRoomAliasAsync("#watercooler:maunium.net");

        var syncHelper = new SyncHelper(hs);

        await hs.GetRoom("!DoHEdFablOLjddKWIp:rory.gay").JoinAsync();

        // foreach (var room in await hs.GetJoinedRooms()) {
        //     if(room.RoomId is "!OGEhHVWSdvArJzumhm:matrix.org") continue;
        //     foreach (var stateEvent in await room.GetStateAsync<List<MatrixEvent>>("")) {
        //         var _ = stateEvent.GetType;
        //     }
        //     _logger.LogInformation($"Got room state for {room.RoomId}!");
        // }

        await syncHelper.RunSyncLoopAsync(cancellationToken: cancellationToken);
    }

    /// <summary>Triggered when the application host is performing a graceful shutdown.</summary>
    /// <param name="cancellationToken">Indicates that the shutdown process should no longer be graceful.</param>
    public Task StopAsync(CancellationToken cancellationToken) {
        _logger.LogInformation("Shutting down bot!");
        return Task.CompletedTask;
    }
}