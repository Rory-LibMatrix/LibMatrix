using System.Collections;
using System.Diagnostics.CodeAnalysis;
using ArcaneLibs.Extensions;

namespace LibMatrix.HomeserverEmulator.Services;

public class HSEConfiguration {
    private static ILogger<HSEConfiguration> _logger;
    public static HSEConfiguration Current { get; set; }

    [RequiresUnreferencedCode("Uses reflection binding")]
    public HSEConfiguration(ILogger<HSEConfiguration> logger, IConfiguration config, HostBuilderContext host) {
        Current = this;
        _logger = logger;
        logger.LogInformation("Loading configuration for environment: {}...", host.HostingEnvironment.EnvironmentName);
        config.GetSection("HomeserverEmulator").Bind(this);
        if (StoreData) {
            DataStoragePath = ExpandPath(DataStoragePath ?? throw new NullReferenceException("DataStoragePath is not set"));
            CacheStoragePath = ExpandPath(CacheStoragePath ?? throw new NullReferenceException("CacheStoragePath is not set"));
        }

        _logger.LogInformation("Configuration loaded: {}", this.ToJson());
    }

    public string CacheStoragePath { get; set; }

    public string DataStoragePath { get; set; }

    public bool StoreData { get; set; } = true;
    
    public bool UnknownSyncTokenIsInitialSync { get; set; } = true;

    private static string ExpandPath(string path, bool retry = true) {
        _logger.LogInformation("Expanding path `{}`", path);

        if (path.StartsWith('~')) {
            path = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[1..]);
        }

        Environment.GetEnvironmentVariables().Cast<DictionaryEntry>().OrderByDescending(x => x.Key.ToString()!.Length).ToList().ForEach(x => {
            path = path.Replace($"${x.Key}", x.Value.ToString());
        });

        _logger.LogInformation("Expanded path to `{}`", path);
        var tries = 0;
        while (retry && path.ContainsAnyOf("~$".Split())) {
            if (tries++ > 100)
                throw new Exception($"Path `{path}` contains unrecognised environment variables");
            path = ExpandPath(path, false);
        }
        
        if(path.StartsWith("./"))
            path = Path.Join(Directory.GetCurrentDirectory(), path[2..].Replace("/", Path.DirectorySeparatorChar.ToString()));

        return path;
    }
}