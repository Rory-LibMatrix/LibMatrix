using System.Text.Json;
using System.Text.Json.Nodes;
using LibMatrix.EventTypes.Spec;
using LibMatrix.Utilities.Bot.Interfaces;

namespace LibMatrix.Utilities.Bot.Commands;

public class PingCommand : ICommand {
    public string Name { get; } = "ping";
    public string[]? Aliases { get; } = [ ];
    public string Description { get; } = "Pong!";
    public bool Unlisted { get; }

    public async Task Invoke(CommandContext ctx) {
        await ctx.Room.SendMessageEventAsync(new RoomMessageEventContent(body: "pong!") {
            AdditionalData = new() {
                // maubot ping compatibility
                ["pong"] = new {
                    ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - ctx.MessageEvent.OriginServerTs,
                    from = ctx.Homeserver.ServerName,
                    ping = ctx.MessageEvent.EventId
                }
            }
        });
    }
}