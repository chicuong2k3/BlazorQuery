
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

    public bool IsFetchingBackground { get; private set; }
    public bool IsLoading => FetchStatus == FetchStatus.Fetching
                                && Data == null;

    public int FailureCount { get; private set; }
    public bool IsRefetchError { get; private set; }

    public event Action? OnChange;
    private readonly Action _onlineStatusHandler;
    private CancellationTokenSource? _staleTimerCts;

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
            await Task.Yield();
            await OnOnlineStatusChangedAsync().ConfigureAwait(false);
        }
        catch
        {
            // ignore
        }
    }
    private Task OnOnlineStatusChangedAsync()
    {
        if (!_onlineManager.IsOnline)
            return Task.CompletedTask;

        // if there's already a fetch in progress, don't start another
        if (FetchStatus == FetchStatus.Fetching)
            return Task.CompletedTask;

        var entry = _client.GetCacheEntry(_queryOptions.QueryKey);
        var isDataStale = entry == null || (_queryOptions.StaleTime > TimeSpan.Zero &&
                                        (DateTime.UtcNow - entry.FetchTime) > _queryOptions.StaleTime);

        if ((_queryOptions.NetworkMode == NetworkMode.Online && FetchStatus == FetchStatus.Paused) ||
            (_queryOptions.NetworkMode == NetworkMode.OfflineFirst && (entry == null || isDataStale)))
        {
            _ = ExecuteAsync();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Executes the query manually.
    /// </summary>
    public async Task ExecuteAsync(CancellationToken? signal = null)
    {
        await _fetchLock.WaitAsync();
        CancellationTokenSource? linkedCts = null;

        bool isRefetch = false;

        try
        {
            _staleTimerCts?.Cancel();

            // Cancel _currentCts only if a fetch is already running
            if (_currentCts != null && FetchStatus == FetchStatus.Fetching)
            {
                _currentCts.Cancel();
                _currentCts.Dispose();
                _currentCts = null;
            }
            _currentCts = new CancellationTokenSource();

            var token = _currentCts.Token;

            if (signal.HasValue)
            {
                linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, signal.Value);
                token = linkedCts.Token;
            }

            var entry = _client.GetCacheEntry(_queryOptions.QueryKey);

            // Optimistically set data from cache if it exists
            if (entry != null && entry.Data is T cached)
            {
                Data = cached;
            }


            var isDataStale = entry == null || (_queryOptions.StaleTime > TimeSpan.Zero &&
                                            (DateTime.UtcNow - entry.FetchTime) > _queryOptions.StaleTime);

            var shouldPause = _queryOptions.NetworkMode != NetworkMode.Always 
                                && !_onlineManager.IsOnline;

            if (shouldPause)
            {
                FetchStatus = FetchStatus.Paused;
                return;
            }

            // If data is fresh, no need to fetch
            if (!isDataStale)
            {
                return;
            }

            // Background fetch: data exists but stale
            isRefetch = Data != null && isDataStale;
            if (isRefetch)
            {
                IsFetchingBackground = true;
                Notify();
            }

            FetchStatus = FetchStatus.Fetching;

            FailureCount = 0;
            IsRefetchError = false;
            Exception? lastError = null;
            int maxRetries = _queryOptions.Retry ?? 0;

            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var ctx = new QueryFunctionContext(_queryOptions.QueryKey, token);
                    Data = await _client.FetchAsync(_queryOptions.QueryKey,
                                                    _ => _queryOptions.QueryFn(ctx),
                                                    _queryOptions.StaleTime,
                                                    token);
                    Error = null;

                    if (entry != null)
                        entry.FetchTime = DateTime.UtcNow;

                    // start the timer so we refetch automatically when stale
                    if (isRefetch == false)
                    {
                        StartStaleTimer();
                    }

                    return;
                }
                catch (OperationCanceledException)
                {
                    if (_queryOptions.NetworkMode != NetworkMode.Always)
                    {
                        FetchStatus = FetchStatus.Paused;
                        IsFetchingBackground = false;
                        return;
                    }
                    throw; 
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    FailureCount++;
                    if (attempt < maxRetries)
                    {
                        await Task.Delay((int)Math.Pow(2, attempt) * 1000, token);
                    }
                }
            }

            Error = lastError;
            IsRefetchError = isRefetch;
        }
        finally
        {
            if (!isRefetch)
            {
                IsFetchingBackground = false;
            }

            if (FetchStatus != FetchStatus.Paused)
            {
                FetchStatus = FetchStatus.Idle;
            }

            linkedCts?.Dispose();
            _fetchLock.Release();
        }
    }

    public async Task RefetchAsync(CancellationToken? signal = null)
    {
        _client.Invalidate(_queryOptions.QueryKey);
        await ExecuteAsync(signal).ConfigureAwait(false);
    }

    private void Notify() => OnChange?.Invoke();

    public void HandleOffline()
    {
        // Cancel any ongoing fetch
        if (_currentCts != null && FetchStatus == FetchStatus.Fetching)
        {
            try
            {
                _currentCts.Cancel();
            }
            finally
            {
                _currentCts.Dispose();
                _currentCts = null;
            }
        }

        FetchStatus = FetchStatus.Paused;
    }

    /// <summary>
    /// Starts a timer that will trigger a background refetch
    /// when the cached data becomes stale.
    /// </summary>
    private void StartStaleTimer()
    {
        if (_staleTimerCts != null && !_staleTimerCts.IsCancellationRequested)
            return;

        // Cancel any previous timer to prevent overlapping refetches
        _staleTimerCts?.Cancel();
        _staleTimerCts?.Dispose();
        _staleTimerCts = null;

        if (_queryOptions.StaleTime <= TimeSpan.Zero)
            return;  

        _staleTimerCts = new CancellationTokenSource();

        // ContinueWith runs on the thread-pool so we don't block the UI.
        _ = Task.Delay(_queryOptions.StaleTime, _staleTimerCts.Token)
                .ContinueWith(t =>
                {
                    // If the delay was cancelled then do nothing
                    if (t.IsCanceled) return;

                    if (_onlineManager.IsOnline && FetchStatus == FetchStatus.Idle)
                    {
                        IsFetchingBackground = true;
                        Notify();
                        _ = ExecuteAsync();
                    }
                }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
    }

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
