namespace BlazorQuery.Core;

/// <summary>
/// Provides options for a query.
/// </summary>
public class QueryOptions<T>
{
    public QueryOptions(
        QueryKey queryKey, 
        Func<QueryFunctionContext, Task<T>> queryFn, 
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
        IReadOnlyDictionary<string, object>? meta = null)
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
    }

    public QueryKey QueryKey { get; init; } = null!;
    public Func<QueryFunctionContext, Task<T>> QueryFn { get; init; } = null!;
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
                        IReadOnlyDictionary<string, object>? meta = null) : base(
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
                            meta)
    {
    }
}