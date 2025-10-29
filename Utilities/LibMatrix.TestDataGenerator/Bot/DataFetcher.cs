using ArcaneLibs.Extensions;
using LibMatrix.EventTypes.Spec;
using LibMatrix.Homeservers;
using LibMatrix.RoomTypes;
using LibMatrix.Utilities.Bot;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

namespace TestDataGenerator.Bot;

public class DataFetcher(AuthenticatedHomeserverGeneric hs, ILogger<DataFetcher> logger, LibMatrixBotConfiguration botConfiguration) : IHostedService {
    private Task? _listenerTask;

    private GenericRoom? _logRoom;

    /// <summary>Triggered when the application host is ready to start the service.</summary>
    /// <param name="cancellationToken">Indicates that the start process has been aborted.</param>
    public async Task StartAsync(CancellationToken cancellationToken) {
        _listenerTask = Run(cancellationToken);
        logger.LogInformation("Bot started!");
    }

    private async Task Run(CancellationToken cancellationToken) {
        Directory.GetFiles("bot_data/cache").ToList().ForEach(File.Delete);
        _logRoom = hs.GetRoom(botConfiguration.LogRoom!);

        await _logRoom.SendMessageEventAsync(new RoomMessageEventContent(body: "Test data collector started!"));
        await _logRoom.SendMessageEventAsync(new RoomMessageEventContent(body: "Fetching rooms..."));

        var rooms = await hs.GetJoinedRooms();
        await _logRoom.SendMessageEventAsync(new RoomMessageEventContent(body: $"Fetched {rooms.Count} rooms!"));

        await _logRoom.SendMessageEventAsync(new RoomMessageEventContent(body: "Fetching room data..."));

        var roomAliasTasks = rooms.Select(room => room.GetCanonicalAliasAsync()).ToAsyncResultEnumerable();
        List<Task<(string, string)>> aliasResolutionTasks = new();
        await foreach (var @event in roomAliasTasks)
            if (@event?.Alias != null) {
                await _logRoom.SendMessageEventAsync(new RoomMessageEventContent(body: $"Fetched room alias {@event.Alias}!"));
                aliasResolutionTasks.Add(Task.Run(async () => {
                    var alias = await hs.ResolveRoomAliasAsync(@event.Alias);
                    return (@event.Alias, alias.RoomId);
                }, cancellationToken));
            }

        var aliasResolutionTaskEnumerator = aliasResolutionTasks.ToAsyncResultEnumerable();
        await foreach (var result in aliasResolutionTaskEnumerator)
            await _logRoom.SendMessageEventAsync(new RoomMessageEventContent(body: $"Resolved room alias {result.Item1} to {result.Item2}!"));
    }

    /// <summary>Triggered when the application host is performing a graceful shutdown.</summary>
    /// <param name="cancellationToken">Indicates that the shutdown process should no longer be graceful.</param>
    public async Task StopAsync(CancellationToken cancellationToken) {
        logger.LogInformation("Shutting down bot!");
        _listenerTask?.Dispose();
    }
}