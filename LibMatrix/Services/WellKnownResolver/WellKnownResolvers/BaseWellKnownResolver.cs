using System.Diagnostics;
using System.Net.Http.Json;
using ArcaneLibs.Collections;
using LibMatrix.Extensions;

namespace LibMatrix.Services.WellKnownResolver.WellKnownResolvers;

public class BaseWellKnownResolver<T> where T : class, new() {
    internal static readonly SemaphoreCache<WellKnownResolverService.WellKnownResolutionResult<T>> WellKnownCache = new() {
        StoreNulls = false
    };

    internal static readonly MatrixHttpClient HttpClient = new();
    
    internal async Task<WellKnownResolverService.WellKnownResolutionResult<T>> TryGetWellKnownFromUrl(string url,
        WellKnownResolverService.WellKnownSource source) {
        var sw = Stopwatch.StartNew();
        try {
            var request = await HttpClient.GetAsync(url);
            sw.Stop();
            var result = new WellKnownResolverService.WellKnownResolutionResult<T> {
                Content = await request.Content.ReadFromJsonAsync<T>(),
                Source = source,
                SourceUri = url,
                Warnings = []
            };

            if (sw.ElapsedMilliseconds > 1000) {
                // logger.LogWarning($"Support well-known resolution took {sw.ElapsedMilliseconds}ms: {url}");
                result.Warnings.Add(new() {
                    Type = WellKnownResolverService.WellKnownResolutionWarning.WellKnownResolutionWarningType.SlowResponse,
                    Message = $"Well-known resolution took {sw.ElapsedMilliseconds}ms"
                });
            }

            return result;
        }
        catch (Exception e) {
            return new WellKnownResolverService.WellKnownResolutionResult<T> {
                Source = source,
                SourceUri = url,
                Warnings = [
                    new() {
                        Exception = e,
                        Type = WellKnownResolverService.WellKnownResolutionWarning.WellKnownResolutionWarningType.Exception,
                        Message = e.Message
                    }
                ]
            };
        }
    }
}