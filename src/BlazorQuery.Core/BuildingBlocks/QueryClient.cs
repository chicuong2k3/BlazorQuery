using System.Collections.Concurrent;

namespace BlazorQuery.Core.BuildingBlocks;

public class QueryClient
{
    private readonly ConcurrentDictionary<QueryKey, CachedQuery> _cache = new();

    public QueryClient()
    {
    }

    public async Task<T> FetchAsync<T>(QueryKey key, Func<Task<T>> fetchFn, TimeSpan? staleTime = null)
    {
        if (_cache.TryGetValue(key, out var cached)
            && !cached.IsStale(staleTime ?? TimeSpan.FromMinutes(5)))
        {
            return (T)cached.Data;
        }

        var data = await fetchFn();
        if (data is null)
        {
            throw new InvalidOperationException("Fetched data cannot be null.");
        }
        _cache[key] = new CachedQuery(data, DateTime.UtcNow);
        return data;
    }

    public void Invalidate(QueryKey key)
    {
        _cache.TryRemove(key, out _);
    }

    private class CachedQuery
    {
        public object Data { get; }
        public DateTime FetchedAt { get; }

        public CachedQuery(object data, DateTime fetchedAt)
        {
            Data = data;
            FetchedAt = fetchedAt;
        }

        public bool IsStale(TimeSpan staleTime)
            => DateTime.UtcNow - FetchedAt > staleTime;
    }
}
