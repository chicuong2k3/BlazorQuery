namespace BlazorQuery.Core;

/// <summary>
/// Provides context for a query function, including its unique <see cref="QueryKey"/>, 
/// an optional <see cref="CancellationToken"/> for cancellation, 
/// and optional metadata (<see cref="Meta"/>). 
/// This context is passed to the query function when executing a query.
/// </summary>
public class QueryFunctionContext(
    QueryKey key,
    CancellationToken signal = default,
    IReadOnlyDictionary<string, object>? meta = null)
{
    public QueryKey QueryKey { get; } = key;
    public CancellationToken Signal { get; } = signal;
    public IReadOnlyDictionary<string, object>? Meta { get; } = meta;

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
}
