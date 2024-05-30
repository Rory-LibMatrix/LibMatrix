using LibMatrix.Filters;
using LibMatrix.Helpers;
using LibMatrix.Homeservers;
using LibMatrix.LegacyEvents.EventTypes.Spec;
using LibMatrix.LegacyEvents.EventTypes.Spec.State;
using LibMatrix.Utilities.Bot.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LibMatrix.Utilities.Bot.Services;

public class CommandListenerHostedService : IHostedService {
    private readonly AuthenticatedHomeserverGeneric _hs;
    private readonly ILogger<CommandListenerHostedService> _logger;
    private readonly IEnumerable<ICommand> _commands;
    private readonly LibMatrixBotConfiguration _config;
    private readonly Func<CommandResult, Task>? _commandResultHandler;

    private Task? _listenerTask;

    public CommandListenerHostedService(AuthenticatedHomeserverGeneric hs, ILogger<CommandListenerHostedService> logger, IServiceProvider services,
        LibMatrixBotConfiguration config, Func<CommandResult, Task>? commandResultHandler = null) {
        logger.LogInformation("{} instantiated!", GetType().Name);
        _hs = hs;
        _logger = logger;
        _config = config;
        _commandResultHandler = commandResultHandler;
        _logger.LogInformation("Getting commands...");
        _commands = services.GetServices<ICommand>();
        _logger.LogInformation("Got {} commands!", _commands.Count());
    }

    /// <summary>Triggered when the application host is ready to start the service.</summary>
    /// <param name="cancellationToken">Indicates that the start process has been aborted.</param>
    public Task StartAsync(CancellationToken cancellationToken) {
        _listenerTask = Run(cancellationToken);
        _logger.LogInformation("Command listener started (StartAsync)!");
        return Task.CompletedTask;
    }

    private async Task? Run(CancellationToken cancellationToken) {
        _logger.LogInformation("Starting command listener!");
        var filter = await _hs.NamedCaches.FilterCache.GetOrSetValueAsync("gay.rory.libmatrix.utilities.bot.command_listener_syncfilter.dev2", new MatrixFilter() {
            AccountData = new MatrixFilter.EventFilter(notTypes: ["*"], limit: 1),
            Presence = new MatrixFilter.EventFilter(notTypes: ["*"]),
            Room = new MatrixFilter.RoomFilter() {
                AccountData = new MatrixFilter.RoomFilter.StateFilter(notTypes: ["*"]),
                Ephemeral = new MatrixFilter.RoomFilter.StateFilter(notTypes: ["*"]),
                State = new MatrixFilter.RoomFilter.StateFilter(notTypes: ["*"]),
                Timeline = new MatrixFilter.RoomFilter.StateFilter(types: ["m.room.message"], notSenders: [_hs.WhoAmI.UserId]),
            }
        });

        var syncHelper = new SyncHelper(_hs, _logger) {
            Timeout = 300_000,
            FilterId = filter
        };

        syncHelper.TimelineEventHandlers.Add(async @event => {
            try {
                var room = _hs.GetRoom(@event.RoomId);
                // _logger.LogInformation(eventResponse.ToJson(indent: false));
                if (@event is { Type: "m.room.message", TypedContent: RoomMessageLegacyEventContent message })
                    if (message is { MessageType: "m.text" }) {
                        var usedPrefix = await GetUsedPrefix(@event);
                        if (usedPrefix is null) return;
                        var res = await InvokeCommand(@event, usedPrefix);
                        await (_commandResultHandler?.Invoke(res) ?? HandleResult(res));
                    }
            }
            catch (Exception e) {
                _logger.LogError(e, "Error in command listener!");
            }
        });
        await syncHelper.RunSyncLoopAsync(cancellationToken: cancellationToken);
    }

    /// <summary>Triggered when the application host is performing a graceful shutdown.</summary>
    /// <param name="cancellationToken">Indicates that the shutdown process should no longer be graceful.</param>
    public async Task StopAsync(CancellationToken cancellationToken) {
        _logger.LogInformation("Shutting down command listener!");
        if (_listenerTask is null) {
            _logger.LogError("Could not shut down command listener task because it was null!");
            return;
        }

        await _listenerTask.WaitAsync(cancellationToken);
    }

    private async Task<string?> GetUsedPrefix(LegacyMatrixEventResponse evt) {
        var messageContent = evt.TypedContent as RoomMessageLegacyEventContent;
        var message = messageContent!.BodyWithoutReplyFallback;
        var prefix = _config.Prefixes.OrderByDescending(x => x.Length).FirstOrDefault(message.StartsWith);
        if (prefix is null && _config.MentionPrefix) {
            var profile = await _hs.GetProfileAsync(_hs.WhoAmI.UserId);
            var roomProfile = await _hs.GetRoom(evt.RoomId!).GetStateAsync<RoomMemberLegacyEventContent>(RoomMemberLegacyEventContent.EventId, _hs.WhoAmI.UserId);
            if (message.StartsWith(_hs.WhoAmI.UserId + ": ")) prefix = profile.DisplayName + ": ";    // `@bot:server.xyz: `
            else if (message.StartsWith(_hs.WhoAmI.UserId + " ")) prefix = profile.DisplayName + " "; // `@bot:server.xyz `
            else if (!string.IsNullOrWhiteSpace(roomProfile?.DisplayName) && message.StartsWith(roomProfile.DisplayName + ": "))
                prefix = roomProfile.DisplayName + ": "; // `local bot: `
            else if (!string.IsNullOrWhiteSpace(roomProfile?.DisplayName) && message.StartsWith(roomProfile.DisplayName + " "))
                prefix = roomProfile.DisplayName + " ";                                                                                                      // `local bot `
            else if (!string.IsNullOrWhiteSpace(profile.DisplayName) && message.StartsWith(profile.DisplayName + ": ")) prefix = profile.DisplayName + ": "; // `bot: `
            else if (!string.IsNullOrWhiteSpace(profile.DisplayName) && message.StartsWith(profile.DisplayName + " ")) prefix = profile.DisplayName + " ";   // `bot `
        }

        return prefix;
    }

    private async Task<CommandResult> InvokeCommand(LegacyMatrixEventResponse evt, string usedPrefix) {
        var message = evt.TypedContent as RoomMessageLegacyEventContent;
        var room = _hs.GetRoom(evt.RoomId!);

        var commandWithoutPrefix = message.BodyWithoutReplyFallback[usedPrefix.Length..].Trim();
        var ctx = new CommandContext {
            Room = room,
            MessageEvent = @evt,
            Homeserver = _hs,
            Args = commandWithoutPrefix.Split(' ').Length == 1 ? [] : commandWithoutPrefix.Split(' ')[1..],
            CommandName = commandWithoutPrefix.Split(' ')[0]
        };
        try {
            var command = _commands.SingleOrDefault(x => x.Name == commandWithoutPrefix.Split(' ')[0] || x.Aliases?.Contains(commandWithoutPrefix.Split(' ')[0]) == true);
            if (command == null) {
                await room.SendMessageEventAsync(
                    new RoomMessageLegacyEventContent("m.notice", $"Command \"{ctx.CommandName}\" not found!"));
                return new() {
                    Success = false,
                    Result = CommandResult.CommandResultType.Failure_InvalidCommand,
                    Context = ctx
                };
            }

            if (await command.CanInvoke(ctx))
                try {
                    await command.Invoke(ctx);
                }
                catch (Exception e) {
                    return new CommandResult() {
                        Context = ctx,
                        Result = CommandResult.CommandResultType.Failure_Exception,
                        Success = false,
                        Exception = e
                    };
                    // await room.SendMessageEventAsync(
                    // MessageFormatter.FormatException("An error occurred during the execution of this command", e));
                }
            else
                return new CommandResult() {
                    Context = ctx,
                    Result = CommandResult.CommandResultType.Failure_NoPermission,
                    Success = false
                };
            // await room.SendMessageEventAsync(
            // new RoomMessageLegacyEventContent("m.notice", "You do not have permission to run this command!"));

            return new CommandResult() {
                Context = ctx,
                Success = true,
                Result = CommandResult.CommandResultType.Success
            };
        }
        catch (Exception e) {
            return new CommandResult() {
                Context = ctx,
                Result = CommandResult.CommandResultType.Failure_Exception,
                Success = false,
                Exception = e
            };
        }
    }

    private async Task HandleResult(CommandResult res) {
        if (res.Success) return;
        var room = res.Context.Room;
        var msg = res.Result switch {
            CommandResult.CommandResultType.Failure_Exception => MessageFormatter.FormatException("An error occurred during the execution of this command", res.Exception!),
            CommandResult.CommandResultType.Failure_NoPermission => new RoomMessageLegacyEventContent("m.notice", "You do not have permission to run this command!"),
            CommandResult.CommandResultType.Failure_InvalidCommand => new RoomMessageLegacyEventContent("m.notice", $"Command \"{res.Context.CommandName}\" not found!"),
            _ => throw new ArgumentOutOfRangeException()
        };

        await room.SendMessageEventAsync(msg);
    }
}