namespace BlazorQuery.Core.Tests.Helpers;

public class QueryObserver<T> : IDisposable
{
    private readonly UseQuery<T> _query;
    public List<QuerySnapshot<T>> Snapshots { get; } = new();

    private TaskCompletionSource<bool>? _tcs;

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
            IsLoading = _query.IsLoading,
            IsFetchingBackground = _query.IsFetchingBackground
        });

        _tcs?.TrySetResult(true); 
    }

    public async Task ExecuteAsync(int waitMs = 0)
    {
        await _query.ExecuteAsync();
        Capture();
        if (waitMs > 0)
            await Task.Delay(waitMs);
    }

    public async Task<QuerySnapshot<T>> WaitForNextSnapshotAsync(Func<QuerySnapshot<T>, bool> predicate, int timeoutMs = 5000)
    {
        if (predicate(Snapshots.LastOrDefault()!))
            return Snapshots.Last();

        _tcs = new TaskCompletionSource<bool>();
        using var cts = new CancellationTokenSource(timeoutMs);
        cts.Token.Register(() => _tcs.TrySetCanceled());

        while (true)
        {
            await _tcs.Task;

            var last = Snapshots.Last();
            if (predicate(last))
                return last;

            _tcs = new TaskCompletionSource<bool>();
        }
    }

    public void Dispose()
    {
        _query.OnChange -= Capture;
    }
}
