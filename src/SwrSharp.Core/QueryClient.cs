using System.Collections.Concurrent;

namespace SwrSharp.Core;

/// <summary>
/// Manages caching, fetching, and invalidation of queries.
/// </summary>
public class QueryClient : IDisposable
{
    public NetworkMode DefaultNetworkMode { get; set; } = NetworkMode.Online;
    public IOnlineManager OnlineManager { get; private set; }
    public IFocusManager FocusManager { get; private set; }
    
    /// <summary>
    /// Logger for audit logging and diagnostics.
    /// </summary>
    public IQueryLogger Logger { get; set; }
    
    /// <summary>
    /// Default value for refetchOnWindowFocus option.
    /// Can be overridden per-query.
    /// </summary>
    public bool DefaultRefetchOnWindowFocus { get; set; } = true;
    
    /// <summary>
    /// Type-specific default query functions.
    /// </summary>
    private readonly ConcurrentDictionary<Type, object> _defaultQueryFns = new();

    /// <summary>
    /// Sets a type-specific default query function.
    /// This provides type-safe default fetching without runtime casts.
    /// </summary>
    /// <typeparam name="T">The return type of the query function.</typeparam>
    /// <param name="queryFn">The default query function for this type.</param>
    public void SetDefaultQueryFn<T>(Func<QueryFunctionContext, Task<T>> queryFn)
    {
        _defaultQueryFns[typeof(T)] = queryFn;
    }

    /// <summary>
    /// Gets the type-specific default query function if registered.
    /// </summary>
    /// <typeparam name="T">The expected return type.</typeparam>
    /// <returns>The registered function or null if not found.</returns>
    internal Func<QueryFunctionContext, Task<T>>? GetDefaultQueryFn<T>()
    {
        if (_defaultQueryFns.TryGetValue(typeof(T), out var fn))
        {
            return fn as Func<QueryFunctionContext, Task<T>>;
        }
        return null;
    }

    /// <summary>
    /// Checks if a default query function is registered for the given type.
    /// </summary>
    internal bool HasDefaultQueryFn<T>() => _defaultQueryFns.ContainsKey(typeof(T));
    
    private int _fetchingQueriesCount = 0;
    
    /// <summary>
    /// Indicates if any queries are currently fetching (including background fetches).
    /// </summary>
    public bool IsFetching => _fetchingQueriesCount > 0;
    
    /// <summary>
    /// Event fired when global fetching state changes.
    /// </summary>
    public event Action? OnFetchingChanged;
    
    public class CacheEntry
    {
        public object? Data { get; set; }
        public Exception? Error { get; set; }
        public DateTime FetchTime { get; set; }
        public Task? OngoingFetch { get; set; }
        
        /// <summary>
        /// Alias for FetchTime to match React Query's dataUpdatedAt property.
        /// </summary>
        public DateTime DataUpdatedAt => FetchTime;
    }

    private readonly ConcurrentDictionary<QueryKey, CacheEntry> _cache = new();

    /// <summary>
    /// Creates a new QueryClient with optional network and focus awareness.
    /// </summary>
    /// <param name="onlineManager">
    /// Optional. Defaults to in-memory manager if not provided.
    /// </param>
    /// <param name="focusManager">
    /// Optional. Defaults to DefaultFocusManager if not provided.
    /// </param>
    /// <param name="logger">
    /// Optional. Logger for audit logging and diagnostics. Defaults to NullQueryLogger.
    /// </param>
    public QueryClient(
        IOnlineManager? onlineManager = null, 
        IFocusManager? focusManager = null,
        IQueryLogger? logger = null)
    {
        OnlineManager = onlineManager ?? new DefaultOnlineManager();
        FocusManager = focusManager ?? new DefaultFocusManager();
        Logger = logger ?? NullQueryLogger.Instance;
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
        catch (OperationCanceledException)
        {
            // Don't persist cancellation as an error in the cache. Throw an exact OperationCanceledException
            entry.Error = null;
            throw new OperationCanceledException(signal ?? CancellationToken.None);
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

    /// <summary>
    /// Gets query state including data and metadata.
    /// Alias for GetCacheEntry.
    /// </summary>
    public CacheEntry? GetQueryState(QueryKey key) => GetCacheEntry(key);

    /// <summary>
    /// Gets cached query data with type safety.
    /// </summary>
    public T? GetQueryData<T>(QueryKey key) => Get<T>(key);

    /// <summary>
    /// Sets query data in cache.
    /// Alias for Set.
    /// </summary>
    public void SetQueryData<T>(QueryKey key, T value) => Set(key, value);

    /// <summary>
    /// Prefetches a query and stores the result in cache.
    /// Useful for preloading data before it's needed.
    /// If the data exists and is not stale, the query will not be executed.
    /// </summary>
    /// <typeparam name="T">The type of data returned by the query.</typeparam>
    /// <param name="options">The query options containing queryKey, queryFn, and other settings.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public async Task PrefetchQueryAsync<T>(QueryOptions<T> options, CancellationToken cancellationToken = default)
    {
        if (options.QueryFn == null)
        {
            throw new InvalidOperationException("QueryFn is required for prefetching");
        }

        var ctx = new QueryFunctionContext(
            options.QueryKey, 
            cancellationToken, 
            options.Meta,
            pageParam: null,
            direction: null,
            client: this);

        await FetchAsync(
            options.QueryKey,
            _ => options.QueryFn(ctx),
            options.StaleTime,
            cancellationToken
        );
    }

    /// <summary>
    /// Invalidates queries matching the filters.
    /// Marks them as stale and triggers refetch if they are currently active.
    /// </summary>
    /// <param name="filters">Optional filters to match specific queries. If null, invalidates all queries.</param>
    public void InvalidateQueries(QueryFilters? filters = null)
    {
        filters ??= new QueryFilters(); // Match all if no filters

        try
        {
            // Find all matching cache entries
            var keysToInvalidate = _cache.Keys
                .Where(key => filters.Matches(key))
                .ToList();

            Logger.LogInformation(
                "Invalidating {Count} queries. Filter: QueryKey={QueryKey}, Exact={Exact}, Type={Type}",
                keysToInvalidate.Count,
                filters.QueryKey?.ToString() ?? "null",
                filters.Exact,
                filters.Type
            );

            foreach (var key in keysToInvalidate)
            {
                if (_cache.TryGetValue(key, out var entry))
                {
                    // Mark as stale by setting FetchTime to distant past
                    // This overrides any staleTime configuration
                    entry.FetchTime = DateTime.MinValue;
                    
                    Logger.LogDebug("Invalidated query: {QueryKey}", key);
                }
            }

            // Fire event for invalidated queries (for active queries to refetch)
            OnQueriesInvalidated?.Invoke(keysToInvalidate);
            
            Logger.LogInformation(
                "Query invalidation completed. {Count} queries invalidated.",
                keysToInvalidate.Count
            );
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error during query invalidation");
            throw;
        }
    }

    /// <summary>
    /// Event fired when queries are invalidated.
    /// Active queries can subscribe to refetch themselves.
    /// </summary>
    public event Action<List<QueryKey>>? OnQueriesInvalidated;

    /// <summary>
    /// Event fired when queries are cancelled.
    /// Active queries can subscribe to handle cancellation.
    /// </summary>
    public event Action<List<QueryKey>>? OnQueriesCancelled;

    /// <summary>
    /// Cancels queries matching the filters.
    /// Cancels ongoing fetches and optionally reverts state.
    /// </summary>
    /// <param name="filters">Optional filters to match specific queries. If null, cancels all queries.</param>
    /// <param name="options">Cancellation options (silent, revert).</param>
    public void CancelQueries(QueryFilters? filters = null, CancelOptions? options = null)
    {
        filters ??= new QueryFilters(); // Match all if no filters
        options ??= new CancelOptions();

        try
        {
            // Find all matching cache entries
            var keysToCancel = _cache.Keys
                .Where(key => filters.Matches(key))
                .ToList();

            Logger.LogInformation(
                "Cancelling {Count} queries. Filter: QueryKey={QueryKey}, Silent={Silent}, Revert={Revert}",
                keysToCancel.Count,
                filters.QueryKey?.ToString() ?? "null",
                options.Silent,
                options.Revert
            );

            // Fire event for cancelled queries (for active queries to cancel themselves)
            OnQueriesCancelled?.Invoke(keysToCancel);
            
            Logger.LogInformation(
                "Query cancellation completed. {Count} queries notified.",
                keysToCancel.Count
            );
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error during query cancellation");
            throw;
        }
    }

    /// <summary>
    /// Internal: Increment global fetching counter.
    /// Called by UseQuery when a fetch starts.
    /// </summary>
    internal void IncrementFetchingQueries()
    {
        var wasFetching = IsFetching;
        Interlocked.Increment(ref _fetchingQueriesCount);
        
        if (!wasFetching && IsFetching)
        {
            OnFetchingChanged?.Invoke();
        }
    }

    /// <summary>
    /// Internal: Decrement global fetching counter.
    /// Called by UseQuery when a fetch completes.
    /// </summary>
    internal void DecrementFetchingQueries()
    {
        var wasFetching = IsFetching;
        Interlocked.Decrement(ref _fetchingQueriesCount);
        
        if (wasFetching && !IsFetching)
        {
            OnFetchingChanged?.Invoke();
        }
    }

    public void Dispose()
    {
        _cache.Clear();
    }

    /// <summary>
    /// Internal helper: returns a snapshot of the cache entries.
    /// Used by UseQuery to inspect existing cached data (e.g. placeholder transitions).
    /// </summary>
    internal IReadOnlyList<KeyValuePair<QueryKey, CacheEntry>> GetAllCacheEntries()
    {
        // Return a snapshot to avoid exposing internal mutable dictionary
        return _cache.ToArray();
    }
}
