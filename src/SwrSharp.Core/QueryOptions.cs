namespace SwrSharp.Core;

/// <summary>
/// Provides options for a query.
/// </summary>
public class QueryOptions<T>
{
    public QueryOptions(
        QueryKey queryKey, 
        Func<QueryFunctionContext, Task<T>>? queryFn = null,
        TimeSpan? staleTime = null, 
        NetworkMode networkMode = NetworkMode.Online,
        bool refetchOnReconnect = true,
        int? retry = null,
        bool retryInfinite = false,
        Func<int, Exception, bool>? retryFunc = null,
        TimeSpan? retryDelay = null,
        TimeSpan? maxRetryDelay = null,
        Func<int, TimeSpan>? retryDelayFunc = null,
        TimeSpan? refetchInterval = null,
        IReadOnlyDictionary<string, object>? meta = null,
        bool enabled = true,
        bool refetchOnWindowFocus = true,
        T? initialData = default,
        Func<T?>? initialDataFunc = null,
        DateTime? initialDataUpdatedAt = null,
        T? placeholderData = default,
        Func<T?, QueryOptions<T>?, T?>? placeholderDataFunc = null)
    {
        QueryKey = queryKey;
        QueryFn = queryFn;
        StaleTime = staleTime ?? TimeSpan.Zero;
        NetworkMode = networkMode;
        RefetchOnReconnect = refetchOnReconnect;
        Retry = retry;
        RetryInfinite = retryInfinite;
        RetryFunc = retryFunc;
        RetryDelay  = retryDelay;
        MaxRetryDelay = maxRetryDelay;
        RetryDelayFunc = retryDelayFunc;
        RefetchInterval = refetchInterval;
        Meta = meta;
        Enabled = enabled;
        RefetchOnWindowFocus = refetchOnWindowFocus;
        InitialData = initialData;
        InitialDataFunc = initialDataFunc;
        InitialDataUpdatedAt = initialDataUpdatedAt;
        PlaceholderData = placeholderData;
        PlaceholderDataFunc = placeholderDataFunc;
    }

    public QueryKey QueryKey { get; init; } = null!;
    public Func<QueryFunctionContext, Task<T>>? QueryFn { get; init; }
    public TimeSpan StaleTime { get; init; } = TimeSpan.Zero;
    public NetworkMode NetworkMode { get; set; } = NetworkMode.Online;
    public bool RefetchOnReconnect { get; set; } = true;
    public int? Retry { get; init; }
    public bool RetryInfinite { get; init; }
    public Func<int, Exception, bool>? RetryFunc { get; init; }
    public TimeSpan? RetryDelay { get; init; }
    public TimeSpan? MaxRetryDelay { get; init; }
    public Func<int, TimeSpan>? RetryDelayFunc { get; init; }
    public TimeSpan? RefetchInterval { get; init; }
    public IReadOnlyDictionary<string, object>? Meta { get; init; }
    public bool Enabled { get; set; } = true;
    public bool RefetchOnWindowFocus { get; set; } = true;

    /// <summary>
    /// Initial data to prepopulate the query cache.
    /// This data is persisted to cache and treated as fresh.
    /// </summary>
    public T? InitialData { get; init; }
    
    /// <summary>
    /// Function to compute initial data lazily (only called once on initialization).
    /// Useful for expensive computations.
    /// </summary>
    public Func<T?>? InitialDataFunc { get; init; }
    
    /// <summary>
    /// Timestamp when the initial data was last updated.
    /// Used with staleTime to determine if data needs refetching.
    /// </summary>
    public DateTime? InitialDataUpdatedAt { get; init; }
    
    /// <summary>
    /// Placeholder data to display while fetching actual data.
    /// NOT persisted to cache. Useful for partial/preview data.
    /// </summary>
    public T? PlaceholderData { get; init; }
    
    /// <summary>
    /// Function to compute placeholder data.
    /// Receives previousData and previousQuery for transitions.
    /// NOT persisted to cache.
    /// </summary>
    public Func<T?, QueryOptions<T>?, T?>? PlaceholderDataFunc { get; init; }
}

public class QueryOptions : QueryOptions<object?>
{
    public QueryOptions(QueryKey queryKey,
                        Func<QueryFunctionContext, Task<object?>> queryFn,
                        TimeSpan? staleTime = null,
                        NetworkMode networkMode = NetworkMode.Online,
                        bool refetchOnReconnect = true,
                        int? retry = null,
                        bool retryInfinite = false,
                        Func<int, Exception, bool>? retryFunc = null,
                        TimeSpan? retryDelay = null,
                        TimeSpan? maxRetryDelay = null,
                        Func<int, TimeSpan>? retryDelayFunc = null,
                        TimeSpan? refetchInterval = null,
                        IReadOnlyDictionary<string, object>? meta = null,
                        bool enabled = true,
                        bool refetchOnWindowFocus = true,
                        object? initialData = default,
                        Func<object?>? initialDataFunc = null,
                        DateTime? initialDataUpdatedAt = null,
                        object? placeholderData = default,
                        Func<object?, QueryOptions<object?>?, object?>? placeholderDataFunc = null) : base(
                            queryKey,
                            queryFn,
                            staleTime,
                            networkMode,
                            refetchOnReconnect,
                            retry,
                            retryInfinite,
                            retryFunc,
                            retryDelay,
                            maxRetryDelay,
                            retryDelayFunc,
                            refetchInterval,
                            meta,
                            enabled,
                            refetchOnWindowFocus,
                            initialData,
                            initialDataFunc,
                            initialDataUpdatedAt,
                            placeholderData,
                            placeholderDataFunc)
    {
    }
}