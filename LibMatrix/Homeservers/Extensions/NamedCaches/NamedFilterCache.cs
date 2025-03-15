using System.Text.Json.Nodes;
using ArcaneLibs.Extensions;
using LibMatrix.Filters;
using LibMatrix.Utilities;

namespace LibMatrix.Homeservers.Extensions.NamedCaches;

public class NamedFilterCache(AuthenticatedHomeserverGeneric hs) : NamedCache<string>(hs, "gay.rory.libmatrix.named_cache.filter") {
    /// <summary>
    ///   <inheritdoc cref="NamedCache{T}.GetOrSetValueAsync"/>
    ///   Allows passing a filter directly, or using a common filter.
    ///   Substitutes @me for the user's ID.
    /// </summary>
    /// <param name="key">Filter name</param>
    /// <param name="filter">Filter to upload if not cached, otherwise defaults to common filters if that exists.</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public async Task<string> GetOrSetValueAsync(string key, SyncFilter? filter = null) {
        var existingValue = await GetValueAsync(key);
        if (existingValue != null) {
            try {
                var existingFilter = await hs.GetFilterAsync(existingValue);
                return existingValue;
            }
            catch {
                // ignored
            }
        }

        if (filter is null) {
            if(CommonSyncFilters.FilterMap.TryGetValue(key, out var commonFilter)) {
                filter = commonFilter;
            } else {
                throw new ArgumentNullException(nameof(filter));
            }
        }

        var filterUpload = await hs.UploadFilterAsync(filter);
        return await SetValueAsync(key, filterUpload.FilterId);
    }
}