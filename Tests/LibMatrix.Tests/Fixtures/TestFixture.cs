using LibMatrix.Services;
using LibMatrix.Tests.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Microsoft.DependencyInjection;
using Xunit.Microsoft.DependencyInjection.Abstracts;

namespace LibMatrix.Tests.Fixtures;

public class TestFixture : TestBedFixture {
    protected override void AddServices(IServiceCollection services, IConfiguration configuration) {
        // services.AddSingleton<TieredStorageService>(x =>
        //     new TieredStorageService(
        //         null,
        //         null
        //     )
        // );
        services.AddSingleton(configuration);

        services.AddRoryLibMatrixServices();
        services.AddLogging();
        services.AddSingleton<HomeserverAbstraction>();
        services.AddSingleton<Config>();
    }

    protected override ValueTask DisposeAsyncCore()
        => new();

    protected override IEnumerable<TestAppSettings> GetTestAppSettings() {
        yield return new TestAppSettings { Filename = "appsettings.json", IsOptional = true };
    }
}