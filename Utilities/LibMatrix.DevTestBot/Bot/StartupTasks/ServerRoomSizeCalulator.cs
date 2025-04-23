using System.Diagnostics.CodeAnalysis;
using LibMatrix.Homeservers;
using LibMatrix.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LibMatrix.ExampleBot.Bot.StartupTasks;

public class ServerRoomSizeCalulator : IHostedService {
    private readonly HomeserverProviderService _homeserverProviderService;
    private readonly ILogger<ServerRoomSizeCalulator> _logger;
    private readonly DevTestBotConfiguration _configuration;

    public ServerRoomSizeCalulator(HomeserverProviderService homeserverProviderService, ILogger<ServerRoomSizeCalulator> logger,
        DevTestBotConfiguration configuration) {
        logger.LogInformation("Server room size calculator hosted service instantiated!");
        _homeserverProviderService = homeserverProviderService;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>Triggered when the application host is ready to start the service.</summary>
    /// <param name="cancellationToken">Indicates that the start process has been aborted.</param>
    [SuppressMessage("ReSharper", "FunctionNeverReturns")]
    public async Task StartAsync(CancellationToken cancellationToken) {
        Directory.GetFiles("bot_data/cache").ToList().ForEach(File.Delete);
        AuthenticatedHomeserverGeneric hs;
        try {
            hs = await _homeserverProviderService.GetAuthenticatedWithToken(_configuration.Homeserver,
                _configuration.AccessToken);
        }
        catch (Exception e) {
            _logger.LogError("{}", e.Message);
            throw;
        }

        await hs.GetRoom("!DoHEdFablOLjddKWIp:rory.gay").JoinAsync();

        Dictionary<string, int> totalRoomSize = new();
        foreach (var room in await hs.GetJoinedRooms()) {
            var stateList = room.GetFullStateAsync().ToBlockingEnumerable().ToList();
            var roomSize = stateList.Count;
            if (roomSize > 10000) await File.AppendAllLinesAsync("large_rooms.txt", new[] { $"{{ \"{room.RoomId}\", {roomSize} }}," }, cancellationToken);

            var roomHs = room.RoomId.Split(":")[1];
            if (totalRoomSize.ContainsKey(roomHs))
                totalRoomSize[roomHs] += roomSize;
            else
                totalRoomSize.Add(roomHs, roomSize);

            _logger.LogInformation($"Got room state for {room.RoomId}!");
        }

        await File.WriteAllTextAsync("server_size.txt", string.Join('\n', totalRoomSize.Select(x => $"{{ \"{x.Key}\", {x.Value} }},")), cancellationToken);
    }

    /// <summary>Triggered when the application host is performing a graceful shutdown.</summary>
    /// <param name="cancellationToken">Indicates that the shutdown process should no longer be graceful.</param>
    public Task StopAsync(CancellationToken cancellationToken) {
        _logger.LogInformation("Shutting down bot!");
        return Task.CompletedTask;
    }
}