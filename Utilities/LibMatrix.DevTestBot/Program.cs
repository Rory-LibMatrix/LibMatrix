// See https://aka.ms/new-console-template for more information

using LibMatrix.ExampleBot.Bot;
using LibMatrix.Services;
using LibMatrix.Utilities.Bot;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

Console.WriteLine("Hello, World!");

var host = Host.CreateDefaultBuilder(args).ConfigureServices((_, services) => {
    services.AddScoped<DevTestBotConfiguration>();
    services.AddRoryLibMatrixServices();

    services.AddMatrixBot()
        .AddCommandHandler()
        .DiscoverAllCommands()
        .WithInviteHandler(ctx => Task.FromResult(ctx.MemberEvent.Sender!.EndsWith(":rory.gay")));

    // services.AddHostedService<ServerRoomSizeCalulator>();
    services.AddHostedService<PingTestBot>();
}).UseConsoleLifetime().Build();

await host.RunAsync();