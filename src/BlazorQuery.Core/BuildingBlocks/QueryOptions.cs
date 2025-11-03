namespace BlazorQuery.Core.BuildingBlocks;

public class QueryOptions<T>
{
    public QueryOptions(
        QueryKey queryKey, 
        Func<QueryFunctionContext, Task<T>> queryFn, 
        TimeSpan? staleTime = null, 
        NetworkMode networkMode = NetworkMode.Online)
    {
        QueryKey = queryKey;
        QueryFn = queryFn;
        StaleTime = staleTime;
        NetworkMode = networkMode;
    }

    public QueryKey QueryKey { get; set; } = null!;
    public Func<QueryFunctionContext, Task<T>> QueryFn { get; set; } = null!;
    public TimeSpan? StaleTime { get; set; }
    public NetworkMode NetworkMode { get; set; } = NetworkMode.Online;
}
