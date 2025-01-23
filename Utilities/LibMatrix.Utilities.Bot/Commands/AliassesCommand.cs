using System.Text;
using LibMatrix.EventTypes.Spec;
using LibMatrix.Helpers;
using LibMatrix.Utilities.Bot.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace LibMatrix.Utilities.Bot.Commands;

public class AliassesCommand(IServiceProvider services) : ICommand {
    public string Name { get; } = "aliasses";
    public string[]? Aliases { get; }
    public string Description { get; } = "Displays aliasses for a command";
    public bool Unlisted { get; } = true;
    //TODO: implement command

    public async Task Invoke(CommandContext ctx) {
        var sb = new StringBuilder();
        sb.AppendLine("Available commands:");
        var commands = services.GetServices<ICommand>().Where(x => !x.Unlisted).ToList();
        foreach (var command in commands) sb.AppendLine($"- {command.Name}: {command.Description}");

        await ctx.Room.SendMessageEventAsync(new RoomMessageEventContent("m.notice", sb.ToString()));
        
        var msb = new MessageBuilder("m.notice");
        msb.WithHtmlTag("table", tb => {
            tb.WithHtmlTag("thead", th => th.WithBody("Available commands"));
            tb.WithHtmlTag("tr", tr => {
                tr.WithHtmlTag("th", th => th.WithBody("Command"));
                tr.WithHtmlTag("th", th => th.WithBody("Aliasses"));
                tr.WithHtmlTag("th", th => th.WithBody("Description"));
            });
            foreach (var command in commands) {
                tb.WithHtmlTag("tr", tr => {
                    tr.WithHtmlTag("td", td => td.WithBody(command.Name));
                    tr.WithHtmlTag("td", td => td.WithBody(string.Join(", ", command.Aliases)));
                    tr.WithHtmlTag("td", td => td.WithBody(command.Description));
                });
            }
        });
        await ctx.Room.SendMessageEventAsync(msb.Build());
    }
}