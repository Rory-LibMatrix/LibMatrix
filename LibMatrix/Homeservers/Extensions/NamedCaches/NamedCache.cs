namespace LibMatrix.Homeservers.Extensions.NamedCaches;

public class NamedCache<T>(AuthenticatedHomeserverGeneric hs, string name) where T : class {
    private Dictionary<string, T>? _cache = new();
    private DateTime _expiry = DateTime.MinValue;
    private SemaphoreSlim _lock = new(1, 1);

    public TimeSpan ExpiryTime { get; set; } = TimeSpan.FromMinutes(5);
    public DateTime GetCurrentExpiryTime() => _expiry;

    /// <summary>
    /// Update the cached map with the latest data from the homeserver.
    /// </summary>
    /// <returns>The updated data</returns>
    public async Task<Dictionary<string, T>> ReadCacheMapAsync() {
        try {
            _cache = await hs.GetAccountDataAsync<Dictionary<string, T>>(name);
        }
        catch (MatrixException e) {
            if (e is { ErrorCode: MatrixException.ErrorCodes.M_NOT_FOUND })
                _cache = [];
            else throw;
        }

        return _cache ?? new();
    }

    public async Task<Dictionary<string, T>> ReadCacheMapCachedAsync() {
        await _lock.WaitAsync();
        if (_expiry < DateTime.Now || _cache == null) {
            _cache = await ReadCacheMapAsync();
            _expiry = DateTime.Now.Add(ExpiryTime);
        }

        _lock.Release();

        return _cache;
    }

    public virtual async Task<T?> GetValueAsync(string key, bool useCache = true) {
        return (await (useCache ? ReadCacheMapCachedAsync() : ReadCacheMapAsync())).GetValueOrDefault(key);
    }

    public virtual async Task<T> SetValueAsync(string key, T value, bool unsafeUseCache = false) {
        if (!unsafeUseCache)
            await _lock.WaitAsync();
        var cache = await (unsafeUseCache ? ReadCacheMapCachedAsync() : ReadCacheMapAsync());
        cache[key] = value;
        await hs.SetAccountDataAsync(name, cache);

        if (!unsafeUseCache)
            _lock.Release();

        return value;
    }

    public virtual async Task<T> RemoveValueAsync(string key, bool unsafeUseCache = false) {
        if (!unsafeUseCache)
            await _lock.WaitAsync();
        var cache = await (unsafeUseCache ? ReadCacheMapCachedAsync() : ReadCacheMapAsync());
        var removedValue = cache[key];
        cache.Remove(key);
        await hs.SetAccountDataAsync(name, cache);

        if (!unsafeUseCache)
            _lock.Release();

        return removedValue;
    }

    public virtual async Task<T> GetOrSetValueAsync(string key, Func<Task<T>> value, bool unsafeUseCache = false) {
        return (await (unsafeUseCache ? ReadCacheMapCachedAsync() : ReadCacheMapAsync())).GetValueOrDefault(key) ?? await SetValueAsync(key, await value());
    }
}