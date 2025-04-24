using LibMatrix.EventTypes.Spec;
using LibMatrix.Utilities.Bot.Interfaces;

namespace LibMatrix.Utilities.Bot.Commands;

public class PingCommand : ICommand {
    public string Name { get; } = "ping";
    public string[]? Aliases { get; } = [];
    public string Description { get; } = "Pong!";
    public bool Unlisted { get; }

    public async Task Invoke(CommandContext ctx) {
        var latency = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - ctx.MessageEvent.OriginServerTs;
        await ctx.Room.SendMessageEventAsync(new RoomMessageEventContent(body: $"Pong! ({latency} ms)") {
            AdditionalData = new() {
                // maubot ping compatibility
                ["pong"] = new {
                    ms = latency,
                    from = ctx.Homeserver.ServerName,
                    ping = ctx.MessageEvent.EventId
                },
            },
            RelatesTo = new() {
                RelationType = "xyz.maubot.pong",
                EventId = ctx.MessageEvent.EventId,
                AdditionalData = new() {
                    ["ms"] = latency!,
                    ["from"] = ctx.Homeserver.ServerName
                }
            }
        });
    }
}