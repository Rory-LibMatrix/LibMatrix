using LibMatrix.Helpers;
using LibMatrix.Utilities.Bot.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace LibMatrix.Utilities.Bot.Commands;

public class HelpCommand(IServiceProvider services) : ICommand {
    public string Name { get; } = "help";
    public string[]? Aliases { get; } = ["?"];
    public string Description { get; } = "Displays this help message";
    public bool Unlisted { get; }

    public async Task Invoke(CommandContext ctx) {
        var commands = services.GetServices<ICommand>()
            .Where(x => !x.Unlisted
                        && !x.GetType().GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>))
            ).ToList();

        var msb = GenerateCommandList(commands);
        
        await ctx.Room.SendMessageEventAsync(msb.Build());
    }

    public static MessageBuilder GenerateCommandList(List<ICommand> commands, MessageBuilder? msb = null) {
        msb ??= new MessageBuilder("m.notice");
        msb.WithTable(tb => {
            tb.WithTitle("Available commands", 2);
            tb.WithRow(rb => {
                rb.WithCell("Command");
                rb.WithCell("Description");
            });

            foreach (var command in commands) {
                tb.WithRow(rb => {
                    rb.WithCell(command.Name);
                    rb.WithCell(command.Description);
                });
            }
        });
        // msb.WithHtmlTag("table", tb => {
            // tb.WithHtmlTag("thead",
                // th => { th.WithHtmlTag("tr", tr => { tr.WithHtmlTag("th", th => th.WithBody("Available commands"), new Dictionary<string, string> { ["colspan"] = "2" }); }); });
            // tb.WithHtmlTag("tr", tr => {
                // tr.WithHtmlTag("th", th => th.WithBody("Command"));
                // tr.WithHtmlTag("th", th => th.WithBody("Description"));
            // });
            // foreach (var command in commands) {
                // tb.WithHtmlTag("tr", tr => {
                    // tr.WithHtmlTag("td", td => td.WithBody(command.Name));
                    // tr.WithHtmlTag("td", td => td.WithBody(command.Description));
                // });
            // }
        // });
        
        return msb;
    }
}