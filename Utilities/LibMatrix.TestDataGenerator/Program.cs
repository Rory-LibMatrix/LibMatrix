// See https://aka.ms/new-console-template for more information

using LibMatrix.Services;
using LibMatrix.Utilities.Bot;
using LibMatrix.Utilities.Bot.AppServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TestDataGenerator.Bot;

Console.WriteLine("Hello, World!");

var host = Host.CreateDefaultBuilder(args).ConfigureServices((_, services) => {
    services.AddScoped<TieredStorageService>(_ =>
        new TieredStorageService(
            new FileStorageProvider("bot_data/cache/"),
            new FileStorageProvider("bot_data/data/")
        )
    );
    // services.AddSingleton<DataFetcherConfiguration>();
    services.AddSingleton<AppServiceConfiguration>();

    services.AddRoryLibMatrixServices();
    services.AddMatrixBot();//.AddCommandHandler().AddCommands([typeof()]);

    services.AddHostedService<DataFetcher>();
}).UseConsoleLifetime().Build();

await host.RunAsync();