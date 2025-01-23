using System.Diagnostics;
using LibMatrix.EventTypes.Spec;
using LibMatrix.ExampleBot.Bot.Interfaces;
using LibMatrix.Helpers;
using LibMatrix.Services;

namespace ModerationBot.Commands;

public class DbgAniRainbowTest(IServiceProvider services, HomeserverProviderService hsProvider, HomeserverResolverService hsResolver) : ICommand {
    public string Name { get; } = "ani-rainbow";
    public string Description { get; } = "[Debug] animated rainbow :)";

    public async Task<bool> CanInvoke(CommandContext ctx) => ctx.Room.RoomId == "!hLEefBaYvNfJwcTjmt:rory.gay";

    public async Task Invoke(CommandContext ctx) {
        //255 long string
        // var rainbow = "🟥🟧🟨🟩🟦🟪";
        var rainbow = "M";
        var chars = rainbow;
        for (var i = 0; i < 76; i++) chars += rainbow[i % rainbow.Length];

        Task.Run(async () => {
            var i = 0;
            var msg = new MessageBuilder("m.notice").WithRainbowString(chars).Build();
            var msgEvent = await ctx.Room.SendMessageEventAsync(msg);

            while (true) {
                msg = new MessageBuilder("m.notice").WithRainbowString(chars, offset: i * 5).Build();
                if (i % 50 == 0) {
                    msg.NewContent = null;
                    msg.RelatesTo = null;
                    msgEvent = await ctx.Room.SendMessageEventAsync(msg);
                }
                else {
                    msg = msg.SetReplaceRelation<RoomMessageEventContent>(msgEvent.EventId);
                    msg.Body = "";
                    msg.FormattedBody = "";
                }

                var sw = Stopwatch.StartNew();
                await
                    ctx.Room.SendMessageEventAsync(msg);
                await Task.Delay(sw.Elapsed);
                i++;
            }
        });
    }
}