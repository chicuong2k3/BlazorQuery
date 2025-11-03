namespace BlazorQuery.Core.BuildingBlocks;

/// <summary>
/// Provides context for a query function, including its unique <see cref="QueryKey"/>, 
/// an optional <see cref="CancellationToken"/> for cancellation, 
/// and optional metadata (<see cref="Meta"/>). 
/// This context is passed to the query function when executing a query.
/// </summary>
public class QueryFunctionContext
{
    public QueryKey QueryKey { get; }
    public CancellationToken Signal { get; }
    public IReadOnlyDictionary<string, object>? Meta { get; }

    public QueryFunctionContext(QueryKey key,
                                CancellationToken signal = default,
                                IReadOnlyDictionary<string, object>? meta = null)
    {
        QueryKey = key;
        Signal = signal;
        Meta = meta;
    }
}
