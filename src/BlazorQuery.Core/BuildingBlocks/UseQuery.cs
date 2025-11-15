
namespace BlazorQuery.Core.BuildingBlocks;

/// <summary>
/// Represents a single query that fetches data asynchronously and tracks its loading/error state.
/// </summary>
/// <typeparam name="T">The type of data returned by the query function.</typeparam>
public class UseQuery<T> : IDisposable 
{
    private readonly QueryClient _client;
    private readonly IOnlineManager _onlineManager;
    private readonly QueryOptions<T> _queryOptions;
    /// <summary>
    /// Cancellation for mid-fetch cancel on offline/refetch
    /// </summary>
    private CancellationTokenSource? _currentCts;
    /// <summary>
    /// Ensure single-concurrent fetch
    /// </summary>
    private readonly SemaphoreSlim _fetchLock = new(1, 1);
    public NetworkMode NetworkMode => _queryOptions.NetworkMode;
    private FetchStatus _fetchStatus;
    public FetchStatus FetchStatus
    {
        get => _fetchStatus;
        private set
        {
            if (_fetchStatus == value)
                return;
            _fetchStatus = value;
            Notify();
        }
    }
    public bool IsFetching => FetchStatus == FetchStatus.Fetching;
    public bool IsPaused => FetchStatus == FetchStatus.Paused;

    private T? _data;
    public T? Data 
    { 
        get => _data;
        private set
        {
            if (Equals(_data, value))
                return;
            _data = value;
            Notify();
        }
    }
    private Exception? _error;
    public Exception? Error 
    {
        get => _error;
        private set
        {
            if (_error?.Equals(value) ?? value == null)
                return;
            _error = value;
            Notify();
        }
    }

    public QueryStatus Status => Error != null
            ? QueryStatus.Error
            : Data == null
                ? QueryStatus.Pending
                : QueryStatus.Success;

    public bool IsPending => Status == QueryStatus.Pending;
    public bool IsSuccess => Status == QueryStatus.Success;
    public bool IsError => Status == QueryStatus.Error;
    public bool IsInitialLoading => Data == null && IsFetching && Status == QueryStatus.Pending;
    private bool _isBackgroundFetch;
    public bool IsFetchingBackground => _isBackgroundFetch;
    public bool IsLoading => (IsFetching || IsPaused) && Data == null && !IsError;  

    public event Action? OnChange;
    private readonly Action _onlineStatusHandler;

    public UseQuery(
            QueryOptions<T> queryOptions,
            QueryClient client)
    {
        _queryOptions = queryOptions;
        _client = client;
        _onlineManager = client.OnlineManager;
        FetchStatus = FetchStatus.Idle;

        if (_queryOptions.NetworkMode == default)
            _queryOptions.NetworkMode = _client.DefaultNetworkMode;

        if (_queryOptions.NetworkMode == NetworkMode.Always)
            _queryOptions.RefetchOnReconnect = false;

        _onlineStatusHandler = () => _ = SafeHandleOnlineStatusChangedAsync();
        if (_queryOptions.RefetchOnReconnect)
        {
            _onlineManager.OnlineStatusChanged += _onlineStatusHandler;
        }
    }

    private async Task SafeHandleOnlineStatusChangedAsync()
    {
        try
        {
            await OnOnlineStatusChangedAsync();
        }
        catch
        {
        }
    }
    private async Task OnOnlineStatusChangedAsync()
    {
        if (!_onlineManager.IsOnline)
            return;

        // if there's already a fetch in progress, don't start another
        if (IsFetching)
            return;

        var entry = _client.GetCacheEntry(_queryOptions.QueryKey);
        var isDataStale = entry == null || (_queryOptions.StaleTime > TimeSpan.Zero &&
                                        (DateTime.UtcNow - entry.FetchTime) > _queryOptions.StaleTime);

        if ((_queryOptions.NetworkMode == NetworkMode.Online && FetchStatus == FetchStatus.Paused) ||
            (_queryOptions.NetworkMode == NetworkMode.OfflineFirst && (entry == null || isDataStale)))
        {
            await ExecuteAsync();
        }
    }

    /// <summary>
    /// Executes the query.
    /// </summary>
    public async Task ExecuteAsync(CancellationToken? signal = null)
    {
        await _fetchLock.WaitAsync();

        try
        {
            _currentCts?.Cancel();
            _currentCts?.Dispose();
            _currentCts = new CancellationTokenSource();
            using var linkedCts = signal.HasValue
                ? CancellationTokenSource.CreateLinkedTokenSource(_currentCts.Token, signal.Value)
                : CancellationTokenSource.CreateLinkedTokenSource(_currentCts.Token);

            var token = linkedCts.Token;

            var entry = _client.GetCacheEntry(_queryOptions.QueryKey);
            var isDataStale = entry == null || (_queryOptions.StaleTime > TimeSpan.Zero &&
                                            (DateTime.UtcNow - entry.FetchTime) > _queryOptions.StaleTime);

            var shouldPause = (_queryOptions.NetworkMode != NetworkMode.Always &&
                              ((_queryOptions.NetworkMode == NetworkMode.Online && !_onlineManager.IsOnline) ||
                               (_queryOptions.NetworkMode == NetworkMode.OfflineFirst && !_onlineManager.IsOnline && isDataStale)));

            if (shouldPause)
            {
                FetchStatus = FetchStatus.Paused;

                if (entry?.Data is T cached)
                    Data = cached;

                return;
            }

            // Background fetch: data exists but stale
            if (Data != null && isDataStale)
            {
                _isBackgroundFetch = true;
                Notify();
            }

            FetchStatus = FetchStatus.Fetching;

            try
            {
                var ctx = new QueryFunctionContext(_queryOptions.QueryKey, token);
                Data = await _client.FetchAsync(_queryOptions.QueryKey,
                                                _ => _queryOptions.QueryFn(ctx),
                                                _queryOptions.StaleTime,
                                                token);
                Error = null;
            }
            catch (OperationCanceledException) when (
                !_onlineManager.IsOnline 
                && _queryOptions.NetworkMode != NetworkMode.Always)
            {
                FetchStatus = FetchStatus.Paused;
                _isBackgroundFetch = false;
                return;
            }
            catch (Exception ex)
            {
                Error = ex;
            }
            finally
            {
                _isBackgroundFetch = false;
                if (FetchStatus != FetchStatus.Paused)
                {
                    FetchStatus = FetchStatus.Idle;
                }

            }
        }
        finally
        {
            _fetchLock.Release();
        }
    }

    public async Task RefetchAsync(CancellationToken? signal = null)
    {
        _client.Invalidate(_queryOptions.QueryKey);
        await ExecuteAsync(signal).ConfigureAwait(false);
    }

    private void Notify() => OnChange?.Invoke();

    public void Dispose()
    {
        try
        {
            if (_queryOptions.RefetchOnReconnect)
                _onlineManager.OnlineStatusChanged -= _onlineStatusHandler;
        }
        catch { }

        _currentCts?.Cancel();
        _currentCts?.Dispose();
        _fetchLock.Dispose();
    }

}
