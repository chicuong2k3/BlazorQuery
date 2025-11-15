using BlazorQuery.Core.BuildingBlocks;

namespace BlazorQuery.Core.Tests.Helpers;

public class QuerySnapshot<T>
{
    public T? Data { get; set; }
    public Exception? Error { get; set; }
    public FetchStatus FetchStatus { get; set; }
    public QueryStatus Status { get; set; }
    public bool IsFetching { get; set; }
    public bool IsPaused { get; set; }
    public bool IsLoading { get; set; }
    public bool IsInitialLoading { get; set; }
    public bool IsFetchingBackground { get; set; }
}
