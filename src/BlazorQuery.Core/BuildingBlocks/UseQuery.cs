
namespace BlazorQuery.Core.BuildingBlocks;

/// <summary>
/// Represents a single query that fetches data asynchronously and tracks its loading/error state.
/// </summary>
/// <typeparam name="T">The type of data returned by the query function.</typeparam>
public class UseQuery<T> : IDisposable 
{
    private readonly QueryClient _client;
    private readonly QueryOptions<T> _queryOptions;
    public NetworkMode NetworkMode => _queryOptions.NetworkMode;
    public FetchStatus FetchStatus { get; private set; } = FetchStatus.Idle;
    public bool IsFetching => FetchStatus == FetchStatus.Fetching;
    public bool IsPaused => FetchStatus == FetchStatus.Paused;

    public T? Data { get; private set; }
    /// <summary>
    /// Indicates that the query is currently loading data (first fetch or refetch).
    /// Unlike IsFetching, this may be true even if FetchStatus is Paused.
    /// </summary>
    public bool IsLoading => Data == null && !IsError;
    public bool IsSuccess => Error == null;
    public bool IsError => Error != null;
    public Exception? Error { get; private set; }

    public event Action? OnChange;

    public UseQuery(
            QueryOptions<T> queryOptions,
            QueryClient client)
    {
        _queryOptions = queryOptions;
        _client = client;

        if (_queryOptions.NetworkMode == default)
            _queryOptions.NetworkMode = _client.DefaultNetworkMode;

        if (_queryOptions.NetworkMode == NetworkMode.Always)
            _queryOptions.RefetchOnReconnect = false;

        if (_queryOptions.RefetchOnReconnect)
        {
            OnlineManager.OnlineStatusChanged += OnOnlineStatusChanged;
        }
    }

    private async void OnOnlineStatusChanged()
    {
        if (!OnlineManager.IsOnline)
            return;

        var entry = _client.GetCacheEntry(_queryOptions.QueryKey);
        var isDataStale = entry == null || (_queryOptions.StaleTime > TimeSpan.Zero &&
                                        (DateTime.UtcNow - entry.FetchTime) > _queryOptions.StaleTime);

        if (_queryOptions.NetworkMode == NetworkMode.Online && FetchStatus == FetchStatus.Paused)
            await ExecuteAsync();
        else if (_queryOptions.NetworkMode == NetworkMode.OfflineFirst && (Data == null || isDataStale))
            await ExecuteAsync();
    }

    /// <summary>
    /// Executes the query, fetching data if cache is stale or missing.
    /// </summary>
    public async Task ExecuteAsync(CancellationToken? signal = null)
    {
        var token = signal ?? CancellationToken.None;

        var entry = _client.GetCacheEntry(_queryOptions.QueryKey);
        var isDataStale = entry == null || (_queryOptions.StaleTime > TimeSpan.Zero &&
                                        (DateTime.UtcNow - entry.FetchTime) > _queryOptions.StaleTime);

        var shouldPause = (_queryOptions.NetworkMode != NetworkMode.Always &&
                          ((_queryOptions.NetworkMode == NetworkMode.Online && !OnlineManager.IsOnline) ||
                           (_queryOptions.NetworkMode == NetworkMode.OfflineFirst && !OnlineManager.IsOnline && isDataStale)));

        if (shouldPause)
        {
            FetchStatus = FetchStatus.Paused;
            Notify();
            return;
        }

        FetchStatus = FetchStatus.Fetching;
        Notify();

        try
        {
            var ctx = new QueryFunctionContext(_queryOptions.QueryKey, token);
            Data = await _client.FetchAsync(_queryOptions.QueryKey,
                                            _ => _queryOptions.QueryFn(ctx),
                                            _queryOptions.StaleTime,
                                            token);
            Error = null;
        }
        catch (OperationCanceledException) when (!OnlineManager.IsOnline && _queryOptions.NetworkMode != NetworkMode.Always)
        {
            FetchStatus = FetchStatus.Paused;
            Notify();
            return;
        }
        catch (Exception ex)
        {
            Error = ex;
        }
        finally
        {
            if (_queryOptions.NetworkMode != NetworkMode.Always && FetchStatus != FetchStatus.Paused)
                FetchStatus = FetchStatus.Idle;

            Notify();
        }
    }

    /// <summary>
    /// Forces refetching the query, invalidating the cache first.
    /// </summary>
    public async Task RefetchAsync(CancellationToken? signal = null)
    {
        _client.Invalidate(_queryOptions.QueryKey);
        await ExecuteAsync(signal);
    }

    private void Notify() => OnChange?.Invoke();

    public void Dispose()
    {
        OnlineManager.OnlineStatusChanged -= OnOnlineStatusChanged;
    }

}
