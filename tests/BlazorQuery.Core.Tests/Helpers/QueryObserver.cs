using BlazorQuery.Core.BuildingBlocks;

namespace BlazorQuery.Core.Tests.Helpers;

public class QueryObserver<T> : IDisposable
{
    private readonly UseQuery<T> _query;
    public List<QuerySnapshot<T>> Snapshots { get; } = new();

    public QueryObserver(UseQuery<T> query)
    {
        _query = query;
        _query.OnChange += Capture;
    }

    private void Capture()
    {
        Snapshots.Add(new QuerySnapshot<T>
        {
            Data = _query.Data,
            Error = _query.Error,
            FetchStatus = _query.FetchStatus,
            Status = _query.Status,
            IsFetching = _query.IsFetching,
            IsPaused = _query.IsPaused,
            IsLoading = _query.IsLoading,
            IsInitialLoading = _query.IsInitialLoading,
            IsFetchingBackground = _query.IsFetchingBackground
        });
    }

    public async Task ExecuteAsync(int waitMs = 0)
    {
        await _query.ExecuteAsync();
        Capture();
        if (waitMs > 0)
            await Task.Delay(waitMs);
    }

    public void Dispose()
    {
        _query.OnChange -= Capture;
    }
}
