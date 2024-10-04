using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LibMatrix.Services;

public static class ServiceInstaller {
    public static IServiceCollection AddRoryLibMatrixServices(this IServiceCollection services, RoryLibMatrixConfiguration? config = null) {
        //Add config
        services.AddSingleton(config ?? new RoryLibMatrixConfiguration());

        //Add services
        services.AddSingleton<HomeserverResolverService>(sp => new HomeserverResolverService(sp.GetRequiredService<ILogger<HomeserverResolverService>>()));
        services.AddSingleton<HomeserverProviderService>();

        return services;
    }
}

public class RoryLibMatrixConfiguration {
    public string AppName { get; set; } = "Rory&::LibMatrix";
}