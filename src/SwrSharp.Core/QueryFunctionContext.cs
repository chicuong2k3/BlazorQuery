namespace SwrSharp.Core;

/// <summary>
/// Direction of fetch for infinite queries.
/// </summary>
public enum FetchDirection
{
    /// <summary>Fetching forward (next page).</summary>
    Forward,
    /// <summary>Fetching backward (previous page).</summary>
    Backward
}

/// <summary>
/// Provides context for a query function, including its unique <see cref="QueryKey"/>, 
/// an optional <see cref="CancellationToken"/> for cancellation, 
/// optional metadata (<see cref="Meta"/>), page parameter and direction for infinite queries,
/// and access to the <see cref="QueryClient"/>.
/// This context is passed to the query function when executing a query.
/// </summary>
public class QueryFunctionContext(
    QueryKey key,
    CancellationToken signal = default,
    IReadOnlyDictionary<string, object>? meta = null,
    object? pageParam = null,
    FetchDirection? direction = null,
    QueryClient? client = null)
{
    public QueryKey QueryKey { get; } = key;
    public CancellationToken Signal { get; } = signal;
    public IReadOnlyDictionary<string, object>? Meta { get; } = meta;
    
    /// <summary>
    /// The page parameter for infinite queries. 
    /// For regular queries, this is always null.
    /// </summary>
    public object? PageParam { get; } = pageParam;
    
    /// <summary>
    /// The direction of fetch for infinite queries.
    /// 'Forward' when fetching next page, 'Backward' when fetching previous page.
    /// For regular queries, this is always null.
    /// </summary>
    public FetchDirection? Direction { get; } = direction;
    
    /// <summary>
    /// Access to the QueryClient instance.
    /// Useful for accessing cache or other client methods inside query function.
    /// </summary>
    public QueryClient? Client { get; } = client;

    /// <summary>
    /// Deconstructs the context into QueryKey and Signal.
    /// Enables: var (queryKey, signal) = ctx;
    /// </summary>
    public void Deconstruct(out QueryKey queryKey, out CancellationToken signal)
    {
        queryKey = QueryKey;
        signal = Signal;
    }

    /// <summary>
    /// Deconstructs the context into QueryKey, Signal, and Meta.
    /// Enables: var (queryKey, signal, meta) = ctx;
    /// </summary>
    public void Deconstruct(out QueryKey queryKey, out CancellationToken signal, out IReadOnlyDictionary<string, object>? meta)
    {
        queryKey = QueryKey;
        signal = Signal;
        meta = Meta;
    }
    
    /// <summary>
    /// Deconstructs the context into QueryKey, Signal, Meta, and PageParam.
    /// Enables: var (queryKey, signal, meta, pageParam) = ctx;
    /// Useful for infinite queries.
    /// </summary>
    public void Deconstruct(out QueryKey queryKey, out CancellationToken signal, out IReadOnlyDictionary<string, object>? meta, out object? pageParam)
    {
        queryKey = QueryKey;
        signal = Signal;
        meta = Meta;
        pageParam = PageParam;
    }
}
