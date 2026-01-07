using System.Diagnostics;
using System.Text.Json.Serialization;
using LibMatrix.Extensions;
using LibMatrix.Services.WellKnownResolver.WellKnownResolvers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LibMatrix.Services.WellKnownResolver;

public class WellKnownResolverService {
    private readonly MatrixHttpClient _httpClient = new();

    private readonly ILogger<WellKnownResolverService> _logger;
    private readonly ClientWellKnownResolver _clientWellKnownResolver;
    private readonly SupportWellKnownResolver _supportWellKnownResolver;
    private readonly ServerWellKnownResolver _serverWellKnownResolver;
    private readonly PolicyServerWellKnownResolver _policyServerWellKnownResolver;
    private readonly WellKnownResolverConfiguration _configuration;

    public WellKnownResolverService(ILogger<WellKnownResolverService> logger, ClientWellKnownResolver clientWellKnownResolver, SupportWellKnownResolver supportWellKnownResolver,
        WellKnownResolverConfiguration configuration, ServerWellKnownResolver serverWellKnownResolver, PolicyServerWellKnownResolver policyServerWellKnownResolver) {
        _logger = logger;
        _clientWellKnownResolver = clientWellKnownResolver;
        _supportWellKnownResolver = supportWellKnownResolver;
        _configuration = configuration;
        _serverWellKnownResolver = serverWellKnownResolver;
        _policyServerWellKnownResolver = policyServerWellKnownResolver;
        if (logger is NullLogger<WellKnownResolverService>) {
            var stackFrame = new StackTrace(true).GetFrame(1);
            Console.WriteLine(
                $"WARN | Null logger provided to WellKnownResolverService!\n{stackFrame?.GetMethod()?.DeclaringType?.ToString() ?? "null"} at {stackFrame?.GetFileName() ?? "null"}:{stackFrame?.GetFileLineNumber().ToString() ?? "null"}");
        }
    }

    public async Task<WellKnownRecords> TryResolveWellKnownRecords(string homeserver, bool includeClient = true, bool includeServer = true, bool includeSupport = true,
        bool includePolicyServer = true, WellKnownResolverConfiguration? config = null) {
        WellKnownRecords records = new();
        _logger.LogDebug($"Resolving well-knowns for {homeserver}");
        var clientTask = includeClient
            ? _clientWellKnownResolver.TryResolveWellKnown(homeserver, config ?? _configuration)
            : Task.FromResult<WellKnownResolutionResult<ClientWellKnown?>>(null!);
        var serverTask = includeServer
            ? _serverWellKnownResolver.TryResolveWellKnown(homeserver, config ?? _configuration)
            : Task.FromResult<WellKnownResolutionResult<ServerWellKnown?>>(null!);
        var supportTask = includeSupport
            ? _supportWellKnownResolver.TryResolveWellKnown(homeserver, config ?? _configuration)
            : Task.FromResult<WellKnownResolutionResult<SupportWellKnown?>>(null!);
        var policyServerTask = includePolicyServer
            ? _policyServerWellKnownResolver.TryResolveWellKnown(homeserver, config ?? _configuration)
            : Task.FromResult<WellKnownResolutionResult<PolicyServerWellKnown?>>(null!);

        if (includeClient && await clientTask is { } clientResult) records.ClientWellKnown = clientResult;
        if (includeServer && await serverTask is { } serverResult) records.ServerWellKnown = serverResult;
        if (includeSupport && await supportTask is { } supportResult) records.SupportWellKnown = supportResult;
        if (includePolicyServer && await policyServerTask is { } policyServerResult) records.PolicyServerWellKnown = policyServerResult;

        return records;
    }

    public class WellKnownRecords {
        public WellKnownResolutionResult<ClientWellKnown?>? ClientWellKnown { get; set; }
        public WellKnownResolutionResult<ServerWellKnown?>? ServerWellKnown { get; set; }
        public WellKnownResolutionResult<SupportWellKnown?>? SupportWellKnown { get; set; }
        public WellKnownResolutionResult<PolicyServerWellKnown?>? PolicyServerWellKnown { get; set; }
    }

    public class WellKnownResolutionResult<T> {
        public WellKnownSource Source { get; set; }
        public string? SourceUri { get; set; }
        public T? Content { get; set; }
        public List<WellKnownResolutionWarning> Warnings { get; set; } = [];
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum WellKnownSource {
        None,
        Https,
        Dns,
        Http,
        ManualCheck,
        Search
    }

    public struct WellKnownResolutionWarning {
        public WellKnownResolutionWarningType Type { get; set; }
        public string Message { get; set; }

        [JsonIgnore]
        public Exception? Exception { get; set; }

        public string? ExceptionMessage => Exception?.Message;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public enum WellKnownResolutionWarningType {
            None,
            Exception,
            InvalidResponse,
            Timeout,
            SlowResponse
        }
    }
}