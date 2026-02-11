namespace SwrSharp.Core;

/// <summary>
/// Options for infinite queries with pagination support.
/// </summary>
public class InfiniteQueryOptions<TData, TPageParam>
{
    public InfiniteQueryOptions(
        QueryKey queryKey,
        Func<QueryFunctionContext, Task<TData>> queryFn,
        TPageParam initialPageParam,
        Func<TData, List<TData>, TPageParam?, TPageParam?>? getNextPageParam = null,
        Func<TData, List<TData>, TPageParam?, TPageParam?>? getPreviousPageParam = null,
        TimeSpan? staleTime = null,
        NetworkMode networkMode = NetworkMode.Online,
        bool refetchOnReconnect = true,
        int? retry = null,
        bool retryInfinite = false,
        Func<int, Exception, bool>? retryFunc = null,
        TimeSpan? retryDelay = null,
        TimeSpan? maxRetryDelay = null,
        Func<int, TimeSpan>? retryDelayFunc = null,
        IReadOnlyDictionary<string, object>? meta = null,
        bool enabled = true,
        bool refetchOnWindowFocus = true,
        int? maxPages = null)
    {
        QueryKey = queryKey;
        QueryFn = queryFn;
        InitialPageParam = initialPageParam;
        GetNextPageParam = getNextPageParam;
        GetPreviousPageParam = getPreviousPageParam;
        StaleTime = staleTime ?? TimeSpan.Zero;
        NetworkMode = networkMode;
        RefetchOnReconnect = refetchOnReconnect;
        Retry = retry;
        RetryInfinite = retryInfinite;
        RetryFunc = retryFunc;
        RetryDelay = retryDelay;
        MaxRetryDelay = maxRetryDelay;
        RetryDelayFunc = retryDelayFunc;
        Meta = meta;
        Enabled = enabled;
        RefetchOnWindowFocus = refetchOnWindowFocus;
        MaxPages = maxPages;
    }

    public QueryKey QueryKey { get; init; }
    
    /// <summary>
    /// Query function that receives context (including pageParam via ctx.PageParam).
    /// Cast ctx.PageParam to TPageParam to use it.
    /// </summary>
    public Func<QueryFunctionContext, Task<TData>> QueryFn { get; init; }
    
    /// <summary>
    /// Initial page parameter (required).
    /// </summary>
    public TPageParam InitialPageParam { get; init; }
    
    /// <summary>
    /// Function to get the next page param.
    /// Return null/undefined to indicate no more pages.
    /// </summary>
    public Func<TData, List<TData>, TPageParam?, TPageParam?>? GetNextPageParam { get; init; }
    
    /// <summary>
    /// Function to get the previous page param.
    /// Return null/undefined to indicate no previous pages.
    /// </summary>
    public Func<TData, List<TData>, TPageParam?, TPageParam?>? GetPreviousPageParam { get; init; }
    
    public TimeSpan StaleTime { get; init; } = TimeSpan.Zero;
    public NetworkMode NetworkMode { get; set; } = NetworkMode.Online;
    public bool RefetchOnReconnect { get; set; } = true;
    public int? Retry { get; init; }
    public bool RetryInfinite { get; init; }
    public Func<int, Exception, bool>? RetryFunc { get; init; }
    public TimeSpan? RetryDelay { get; init; }
    public TimeSpan? MaxRetryDelay { get; init; }
    public Func<int, TimeSpan>? RetryDelayFunc { get; init; }
    public IReadOnlyDictionary<string, object>? Meta { get; init; }
    public bool Enabled { get; set; } = true;
    public bool RefetchOnWindowFocus { get; set; } = true;
    
    /// <summary>
    /// Maximum number of pages to keep in memory.
    /// Useful for limiting memory usage with large lists.
    /// </summary>
    public int? MaxPages { get; init; }
}

