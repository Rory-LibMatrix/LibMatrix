using LibMatrix.EventTypes.Spec;
using LibMatrix.ExampleBot.Bot.Interfaces;

namespace LibMatrix.ExampleBot.Bot.Commands;

public class PingCommand : ICommand {
    public string Name { get; } = "ping";
    public string Description { get; } = "Pong!";

    // public async Task Invoke(CommandContext ctx) => await ctx.Room.SendMessageEventAsync(new RoomMessageEventContent(body: "pong!"));
    public async Task Invoke(CommandContext ctx) {
        // await ctx.Room.SendMessageEventAsync(new RoomMessageEventContent(body: "pong!"));
        var count = ctx.Args.Length > 0 ? int.Parse(ctx.Args[0]) : 1;
        var tasks = Enumerable.Range(0, count).Select(async i => {
            await ctx.Room.SendMessageEventAsync(new RoomMessageEventContent(body: $"!ping {i}", messageType: "m.text"));
            await Task.Delay(1000);
        }).ToList();
        await Task.WhenAll(tasks);
        
        await ctx.Room.SendMessageEventAsync(new RoomMessageEventContent(body: "Pong!"));
    }
}