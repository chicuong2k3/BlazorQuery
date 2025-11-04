namespace BlazorQuery.Core.BuildingBlocks;

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
        bool refetchOnReconnect = true)
    {
        QueryKey = queryKey;
        QueryFn = queryFn;
        StaleTime = staleTime ?? TimeSpan.Zero;
        NetworkMode = networkMode;
        RefetchOnReconnect = refetchOnReconnect;
    }

    public QueryKey QueryKey { get; init; } = null!;
    public Func<QueryFunctionContext, Task<T>> QueryFn { get; init; } = null!;
    public TimeSpan StaleTime { get; init; } = TimeSpan.Zero;
    public NetworkMode NetworkMode { get; set; } = NetworkMode.Online;
    public bool RefetchOnReconnect { get; set; } = true;
}
