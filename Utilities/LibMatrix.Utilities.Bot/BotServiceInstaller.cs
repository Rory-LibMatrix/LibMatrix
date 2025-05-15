using ArcaneLibs;
using LibMatrix.Homeservers;
using LibMatrix.Services;
using LibMatrix.Utilities.Bot.AppServices;
using LibMatrix.Utilities.Bot.Interfaces;
using LibMatrix.Utilities.Bot.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LibMatrix.Utilities.Bot;

public static class BotServiceInstallerExtensions {
    public static BotServiceInstaller AddMatrixBot(this IServiceCollection services) {
        return new BotServiceInstaller(services).AddMatrixBot();
    }
}

public class BotServiceInstaller(IServiceCollection services) {
    public BotServiceInstaller AddMatrixBot() {
        services.AddSingleton<LibMatrixBotConfiguration>();

        services.AddSingleton<AuthenticatedHomeserverGeneric>(x => {
            var config = x.GetService<LibMatrixBotConfiguration>() ?? throw new Exception("No configuration found!");
            var hsProvider = x.GetService<HomeserverProviderService>() ?? throw new Exception("No homeserver provider found!");

            if (x.GetService<AppServiceConfiguration>() is AppServiceConfiguration appsvcConfig)
                config.AccessToken = appsvcConfig.AppserviceToken;
            else if (Environment.GetEnvironmentVariable("LIBMATRIX_ACCESS_TOKEN_PATH") is string path)
                config.AccessTokenPath = path;

            if (string.IsNullOrWhiteSpace(config.AccessToken) && string.IsNullOrWhiteSpace(config.AccessTokenPath))
                throw new Exception("Unable to add bot service without an access token or access token path!");

            if (!string.IsNullOrWhiteSpace(config.AccessTokenPath)) {
                var token = File.ReadAllText(config.AccessTokenPath);
                config.AccessToken = token.Trim();
            }

            var hs = hsProvider.GetAuthenticatedWithToken(config.Homeserver, config.AccessToken).Result;

            return hs;
        });

        return this;
    }

    public BotServiceInstaller AddCommandHandler() {
        Console.WriteLine("Adding command handler...");
        services.AddSingleton(s => s.GetRequiredService<LibMatrixBotConfiguration>().CommandListener
                                   ?? throw new Exception("Command handling is enabled, but configuration is missing the LibMatrixBot:CommandListener configuration section!")
        );
        services.AddHostedService<CommandListenerHostedService>();
        return this;
    }

    public BotServiceInstaller DiscoverAllCommands() {
        foreach (var commandClass in ClassCollector<ICommand>.ResolveFromAllAccessibleAssemblies()) {
            Console.WriteLine($"Adding command {commandClass.Name}");
            services.AddScoped(typeof(ICommand), commandClass);
        }

        return this;
    }

    public BotServiceInstaller AddCommands(IEnumerable<Type> commandClasses) {
        foreach (var commandClass in commandClasses) {
            if (!commandClass.IsAssignableTo(typeof(ICommand)))
                throw new Exception($"Type {commandClass.Name} is not assignable to ICommand!");
            Console.WriteLine($"Adding command {commandClass.Name}");
            services.AddScoped(typeof(ICommand), commandClass);
        }

        return this;
    }

    public BotServiceInstaller WithCommandResultHandler(Func<CommandResult, Task> commandResultHandler) {
        services.AddSingleton(commandResultHandler);
        return this;
    }

    public BotServiceInstaller WithInviteHandler(Func<RoomInviteContext, Task> inviteHandler) {
        services.AddSingleton(inviteHandler);
        services.AddSingleton(s => s.GetRequiredService<LibMatrixBotConfiguration>().InviteListener
                                   ?? throw new Exception("Invite handling is enabled, but configuration is missing the LibMatrixBot:InviteListener configuration section!")
        );
        services.AddHostedService<InviteHandlerHostedService>();
        return this;
    }

    public BotServiceInstaller WithInviteHandler<T>() where T : class, IRoomInviteHandler {
        services.AddSingleton<T>();
        services.AddSingleton<Func<RoomInviteContext, Task>>(sp => sp.GetRequiredService<T>().HandleInviteAsync);
        services.AddSingleton(s => s.GetRequiredService<LibMatrixBotConfiguration>().InviteListener
                                   ?? throw new Exception("Invite handling is enabled, but configuration is missing the LibMatrixBot:InviteListener configuration section!")
        );
        services.AddHostedService<InviteHandlerHostedService>();
        return this;
    }
}