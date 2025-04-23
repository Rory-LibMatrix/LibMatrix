using ArcaneLibs.StringNormalisation;
using LibMatrix.EventTypes.Spec;
using LibMatrix.Utilities.Bot.Interfaces;

namespace LibMatrix.ExampleBot.Bot.Commands;

public class CmdCommand : ICommand {
    public string Name => "cmd";
    public string[]? Aliases => [];
    public bool Unlisted => false;
    public string Description => "Runs a command on the host system";

    public Task<bool> CanInvoke(CommandContext ctx) => Task.FromResult(ctx.MessageEvent.Sender.EndsWith(":rory.gay") || ctx.MessageEvent.Sender.EndsWith(":conduit.rory.gay"));

    public async Task Invoke(CommandContext ctx) {
        var cmd = ctx.Args.Aggregate("\"", (current, arg) => current + arg + " ");

        cmd = cmd.Trim();
        cmd += "\"";

        await ctx.Room.SendMessageEventAsync(new RoomMessageEventContent(body: $"Command being executed: `{cmd}`"));

        var output = ArcaneLibs.Util.GetCommandOutputAsync(
            Environment.OSVersion.Platform == PlatformID.Unix ? "/bin/sh" : "cmd.exe",
            (Environment.OSVersion.Platform == PlatformID.Unix ? "-c " : "/c ") + cmd);
        // .Replace("`", "\\`")
        // .Split("\n").ToList();

        var msg = "";
        var msgId = await ctx.Room.SendMessageEventAsync(new RoomMessageEventContent {
            FormattedBody = $"Waiting for command output...",
            Body = msg.RemoveAnsi(),
            Format = "m.notice"
        });

        var lastSendTask = Task.CompletedTask;
        await foreach (var @out in output) {
            Console.WriteLine($"{@out.Length:0000} {@out}");
            msg += @out + "\n";
            if (lastSendTask.IsCompleted)
                lastSendTask = ctx.Room.SendMessageEventAsync(new RoomMessageEventContent {
                    FormattedBody = $"<pre class=\"language-csharp\">\n{msg}\n</pre>",
                    Body = msg.RemoveAnsi(),
                    Format = "org.matrix.custom.html"
                });
            if (msg.Length > 31000) {
                await lastSendTask;
                msgId = await ctx.Room.SendMessageEventAsync(new RoomMessageEventContent {
                    FormattedBody = $"Waiting for command output...",
                    Body = msg.RemoveAnsi(),
                    Format = "m.notice"
                });
                msg = "";
            }
        }

        // while (output.Count > 0) {
        //     Console.WriteLine("Adding: " + output[0]);
        //     msg += output[0] + "\n";
        //     output.RemoveAt(0);
        //     if ((output.Count > 0 && (msg + output[0]).Length > 31500) || output.Count == 0) {
        //         await ctx.Room.SendMessageEventAsync("m.room.message", new RoomMessageEventContent {
        //             FormattedBody = $"<pre class=\"language-csharp\">\n{msg}\n</pre>",
        //             // Body = Markdig.Markdown.ToHtml(msg),
        //             Body = msg.RemoveAnsi(),
        //             Format = "org.matrix.custom.html"
        //         });
        //         msg = "";
        //     }
        // }
    }
}