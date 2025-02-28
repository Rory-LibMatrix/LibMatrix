using System.Diagnostics;
using System.Text.Json.Serialization;
using ArcaneLibs.Collections;
using ArcaneLibs.Extensions;
using LibMatrix.Extensions;
using LibMatrix.Services.WellKnownResolvers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LibMatrix.Services;

public class WellKnownResolverService {
    private readonly MatrixHttpClient _httpClient = new();

    private readonly ILogger<WellKnownResolverService> _logger;

    public WellKnownResolverService(ILogger<WellKnownResolverService> logger) {
        _logger = logger;
        if (logger is NullLogger<WellKnownResolverService>) {
            var stackFrame = new StackTrace(true).GetFrame(1);
            Console.WriteLine(
                $"WARN | Null logger provided to WellKnownResolverService!\n{stackFrame?.GetMethod()?.DeclaringType?.ToString() ?? "null"} at {stackFrame?.GetFileName() ?? "null"}:{stackFrame?.GetFileLineNumber().ToString() ?? "null"}");
        }
    }

    public async Task<WellKnownRecords> TryResolveWellKnownRecords(string homeserver) {
        WellKnownRecords records = new();
        _logger.LogDebug($"Resolving well-knowns for {homeserver}");
        
        return records;
    }



    public class ServerWellKnown {
        [JsonPropertyName("m.server")]
        public required string Homeserver { get; set; }
    }

    public class WellKnownRecords {
        public ClientWellKnownResolver.ClientWellKnown? ClientWellKnown { get; set; }
        public ServerWellKnown? ServerWellKnown { get; set; }
        public SupportWellKnownResolver.SupportWellKnown? SupportWellKnown { get; set; }

        /// <summary>
        /// Reports the source of the client well-known data.
        /// </summary>
        public WellKnownSource? ClientWellKnownSource { get; set; }

        /// <summary>
        /// Reports the source of the server well-known data.
        /// </summary>
        public WellKnownSource? ServerWellKnownSource { get; set; }

        /// <summary>
        /// Reports the source of the support well-known data.
        /// </summary>
        public WellKnownSource? SupportWellKnownSource { get; set; }
    }
    
    public struct WellKnownResolutionResult<T> {
        public WellKnownResolverService.WellKnownSource Source { get; set; }
        public T WellKnown { get; set; }
        public List<WellKnownResolverService.WellKnownResolutionWarning> Warnings { get; set; }
    }
    
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
        public Exception? Exception { get; set; }
        
        public enum WellKnownResolutionWarningType {
            None,
            Exception,
            InvalidResponse,
            Timeout
        }
    }
}