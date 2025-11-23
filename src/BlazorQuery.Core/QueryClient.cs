using System.Collections.Concurrent;

namespace BlazorQuery.Core;

/// <summary>
/// Manages caching, fetching, and invalidation of queries.
/// </summary>
public class QueryClient : IDisposable
{
    public NetworkMode DefaultNetworkMode { get; set; } = NetworkMode.Online;
    public IOnlineManager OnlineManager { get; private set; }
    public class CacheEntry
    {
        public object? Data { get; set; }
        public Exception? Error { get; set; }
        public DateTime FetchTime { get; set; }
        public Task? OngoingFetch { get; set; }
    }

    private readonly ConcurrentDictionary<QueryKey, CacheEntry> _cache = new();

    /// <summary>
    /// Creates a new QueryClient with optional network awareness.
    /// </summary>
    /// <param name="onlineManager">
    /// Optional. Defaults to in-memory manager if not provided.
    /// </param>
    public QueryClient(IOnlineManager? onlineManager = null)
    {
        OnlineManager = onlineManager ?? new DefaultOnlineManager();
        DefaultNetworkMode = NetworkMode.Online;
    }

    /// <summary>
    /// Fetches data for a query, using cache if possible and handling concurrent requests.
    /// </summary>
    public async Task<T> FetchAsync<T>(
        QueryKey key,
        Func<CancellationToken, Task<T>> fetchFn,
        TimeSpan? staleTime = null,
        CancellationToken? signal = null)
    {
        var now = DateTime.UtcNow;
        staleTime ??= TimeSpan.Zero;

        var entry = _cache.GetOrAdd(key, _ => new CacheEntry());

        if (entry.Data is T cachedData &&
                entry.Error == null &&
                (now - entry.FetchTime) <= staleTime)
        {
            return cachedData;
        }   

        if (entry.OngoingFetch != null)
        {
            await entry.OngoingFetch;
            if (entry.Error != null) throw entry.Error;
            return (T)entry.Data!;
        }

        var tcs = new TaskCompletionSource();
        entry.OngoingFetch = tcs.Task;

        try
        {
            var result = await fetchFn(signal ?? CancellationToken.None);
            entry.Data = result!;
            entry.Error = null;
            entry.FetchTime = now;
            return result!;
        }
        catch (Exception ex)
        {
            entry.Error = ex;
            throw;
        }
        finally
        {
            tcs.SetResult();
            entry.OngoingFetch = null;
        }
    }

    /// <summary>
    /// Invalidates a cached query.
    /// </summary>
    public void Invalidate(QueryKey key)
    {
        _cache.TryRemove(key, out _);
    }

    /// <summary>
    /// Retrieves cached data without refetching.
    /// </summary>
    public T? Get<T>(QueryKey key)
    {
        if (_cache.TryGetValue(key, out var entry) && entry.Data is T data)
        {
            return data;
        }
        return default;
    }

    /// <summary>
    /// Manually sets cached data.
    /// </summary>
    public void Set<T>(QueryKey key, T value)
    {
        var entry = _cache.GetOrAdd(key, _ => new CacheEntry());
        entry.Data = value!;
        entry.Error = null;
        entry.FetchTime = DateTime.UtcNow;
    }

    public CacheEntry? GetCacheEntry(QueryKey key)
    {
        _cache.TryGetValue(key, out var entry);
        return entry;
    }

    public void Dispose()
    {
        _cache.Clear();
    }
}
