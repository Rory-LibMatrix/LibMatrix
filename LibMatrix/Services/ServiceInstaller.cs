using LibMatrix.Services.WellKnownResolvers;
using Microsoft.Extensions.DependencyInjection;

namespace LibMatrix.Services;

public static class ServiceInstaller {
    public static IServiceCollection AddRoryLibMatrixServices(this IServiceCollection services, RoryLibMatrixConfiguration? config = null) {
        //Add config
        services.AddSingleton(config ?? new RoryLibMatrixConfiguration());

        //Add services
        services.AddSingleton<ClientWellKnownResolver>();
        services.AddSingleton<WellKnownResolverService>();
        // Legacy
        services.AddSingleton<HomeserverResolverService>();
        services.AddSingleton<HomeserverProviderService>();

        return services;
    }
}

public class RoryLibMatrixConfiguration {
    public string AppName { get; set; } = "Rory&::LibMatrix";
}