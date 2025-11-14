using System.Diagnostics.CodeAnalysis;
using System.Windows.Input;
using ArcaneLibs.Extensions;
using LibMatrix.EventTypes.Spec;
using LibMatrix.Filters;
using LibMatrix.Helpers;
using LibMatrix.Homeservers;
using LibMatrix.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LibMatrix.ExampleBot.Bot;

public class PingTestBot : IHostedService {
    private readonly HomeserverProviderService _homeserverProviderService;
    private readonly ILogger<DevTestBot> _logger;
    private readonly DevTestBotConfiguration _configuration;
    private readonly IEnumerable<ICommand> _commands;

    public PingTestBot(HomeserverProviderService homeserverProviderService, ILogger<DevTestBot> logger,
        DevTestBotConfiguration configuration, IServiceProvider services) {
        logger.LogInformation("{} instantiated!", GetType().Name);
        _homeserverProviderService = homeserverProviderService;
        _logger = logger;
        _configuration = configuration;
        _logger.LogInformation("Getting commands...");
        _commands = services.GetServices<ICommand>();
        _logger.LogInformation("Got {} commands!", _commands.Count());
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

        var syncHelper = new SyncHelper(hs);
        syncHelper.Filter = new SyncFilter {
            Room = new SyncFilter.RoomFilter {
                Timeline = new SyncFilter.RoomFilter.StateFilter() {
                    Limit = 1,
                    Senders = ["@me"]
                },
                Rooms = ["!ping-v11:maunium.net"],
                AccountData = new(types: []),
                IncludeLeave = false
            }
        };

        // await hs.GetRoom("!VJwxdebqoQlhGSEncc:codestorm.net").JoinAsync();

        // foreach (var room in await hs.GetJoinedRooms()) {
        //     if(room.RoomId is "!OGEhHVWSdvArJzumhm:matrix.org") continue;
        //     foreach (var stateEvent in await room.GetStateAsync<List<MatrixEvent>>("")) {
        //         var _ = stateEvent.GetType;
        //     }
        //     _logger.LogInformation($"Got room state for {room.RoomId}!");
        // }

        // syncHelper.InviteReceivedHandlers.Add(async Task (args) => {
        //     var inviteEvent =
        //         args.Value.InviteState.Events.FirstOrDefault(x =>
        //             x.Type == "m.room.member" && x.StateKey == hs.UserId);
        //     _logger.LogInformation(
        //         $"Got invite to {args.Key} by {inviteEvent.Sender} with reason: {(inviteEvent.TypedContent as RoomMemberEventContent).Reason}");
        //     if (inviteEvent.Sender.EndsWith(":rory.gay") || inviteEvent.Sender == "@mxidupwitch:the-apothecary.club")
        //         try {
        //             var senderProfile = await hs.GetProfileAsync(inviteEvent.Sender);
        //             await hs.GetRoom(args.Key).JoinAsync(reason: $"I was invited by {senderProfile.DisplayName ?? inviteEvent.Sender}!");
        //         }
        //         catch (Exception e) {
        //             _logger.LogError("{}", e.ToString());
        //             await hs.GetRoom(args.Key).LeaveAsync("I was unable to join the room: " + e);
        //         }
        // });
        // Deprecated, using Bot Utils instead:
        // syncHelper.TimelineEventHandlers.Add(async @event => {
        //     _logger.LogInformation(
        //         "Got timeline event in {}: {}", @event.RoomId, @event.ToJson(false, true));
        //
        //     var room = hs.GetRoom(@event.RoomId);
        //     // _logger.LogInformation(eventResponse.ToJson(indent: false));
        //     if (@event is not { Sender: "@emma:rory.gay" }) return;
        //     if (@event is { Type: "m.room.message", TypedContent: RoomMessageEventContent message })
        //         if (message is { MessageType: "m.text" } && message.Body.StartsWith(_configuration.Prefix)) {
        //             var command = _commands.FirstOrDefault(x => x.Name == message.Body.Split(' ')[0][_configuration.Prefix.Length..]);
        //             if (command == null) {
        //                 await room.SendMessageEventAsync(
        //                     new RoomMessageEventContent("m.text", "Command not found!"));
        //                 return;
        //             }
        //
        //             var ctx = new CommandContext {
        //                 Room = room,
        //                 MessageEvent = @event
        //             };
        //             if (await command.CanInvoke(ctx))
        //                 await command.Invoke(ctx);
        //             else
        //                 await room.SendMessageEventAsync(
        //                     new RoomMessageEventContent("m.text", "You do not have permission to run this command!"));
        //         }
        // });
        await syncHelper.RunSyncLoopAsync(cancellationToken: cancellationToken);
    }

    /// <summary>Triggered when the application host is performing a graceful shutdown.</summary>
    /// <param name="cancellationToken">Indicates that the shutdown process should no longer be graceful.</param>
    public Task StopAsync(CancellationToken cancellationToken) {
        _logger.LogInformation("Shutting down bot!");
        return Task.CompletedTask;
    }
}