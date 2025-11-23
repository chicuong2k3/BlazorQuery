namespace BlazorQuery.Core;

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
    private Exception? _lastError;
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
    private CancellationTokenSource? _refetchIntervalCts;

    private static readonly Random _jitterRandom = new();

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

        // Start interval polling if configured
        if (_queryOptions.RefetchInterval.HasValue)
            StartRefetchInterval();
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

        if (FetchStatus == FetchStatus.Paused)
        {
            _ = ExecuteAsync(); 
            return Task.CompletedTask;
        }

        if (_queryOptions.NetworkMode == NetworkMode.OfflineFirst && isDataStale)
        {
            _ = ExecuteAsync();
            return Task.CompletedTask;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Executes the query manually.
    /// </summary>
    public async Task ExecuteAsync(CancellationToken? signal = null, bool isRefetch = false)
    {
        await _fetchLock.WaitAsync();
        CancellationTokenSource? linkedCts = null;

        try
        {
            _staleTimerCts?.Cancel();

            // Cancel _currentCts only if a fetch is already running
            if (_currentCts != null && FetchStatus == FetchStatus.Fetching)
            {
                _currentCts.Cancel();
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
            bool isBackgroundFetch = !isRefetch && Data != null && isDataStale;
            if (isBackgroundFetch)
            {
                IsFetchingBackground = true;
                Notify();
            }

            FetchStatus = FetchStatus.Fetching;

            if (!isRefetch)
            {
                FailureCount = 0;
                IsRefetchError = false;
                _lastError = null;
            }

            int maxRetries = _queryOptions.Retry ?? 0;
            TimeSpan maxRetryDelay = _queryOptions.MaxRetryDelay ?? TimeSpan.FromSeconds(30);

            for (int attempt = 0;; attempt++)
            {
                try
                {
                    var ctx = new QueryFunctionContext(_queryOptions.QueryKey, token, _queryOptions.Meta);
                    Data = await _client.FetchAsync(_queryOptions.QueryKey,
                                                    _ => _queryOptions.QueryFn(ctx),
                                                    _queryOptions.StaleTime,
                                                    token);

                    _lastError = null;
                    Error = null;

                    if (entry != null)
                        entry.FetchTime = DateTime.UtcNow;

                    // start the timer so we refetch automatically when stale
                    if (!isRefetch)
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
                    FailureCount++;
                    _lastError = ex;
                    bool shouldRetry = false;

                    // infinite retry
                    if (_queryOptions.RetryInfinite) 
                        shouldRetry = true;
                    // retry n times
                    else if (_queryOptions.Retry.HasValue && attempt < _queryOptions.Retry.Value) 
                        shouldRetry = true;
                    // custom retry func
                    else if (_queryOptions.RetryFunc != null) 
                        shouldRetry = _queryOptions.RetryFunc(attempt, ex);

                    if (!shouldRetry)
                    {
                        Error = _lastError;
                        if (isRefetch) IsRefetchError = true;
                        break;
                    }

                    // pause retry if offline
                    if (_queryOptions.NetworkMode != NetworkMode.Always && !_onlineManager.IsOnline)
                    {
                        FetchStatus = FetchStatus.Paused;
                        return;
                    }

                    // delay before retry
                    int delayMs; 
                    if (_queryOptions.RetryDelayFunc != null)
                    {
                        delayMs = (int)_queryOptions.RetryDelayFunc(attempt).TotalMilliseconds;
                    }
                    else
                    {
                        double expDelay = Math.Pow(2, attempt) * 1000; 
                        double jitter = _jitterRandom.NextDouble() * 300; 
                        delayMs = (int)Math.Min(expDelay + jitter, maxRetryDelay.TotalMilliseconds);
                    }

                    await Task.Delay(delayMs, token);
                }
            }

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
            OnChange?.Invoke();
        }
    }

    public async Task RefetchAsync(CancellationToken? signal = null)
    {
        _client.Invalidate(_queryOptions.QueryKey);
        await ExecuteAsync(signal, isRefetch: true).ConfigureAwait(false);
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
            catch { }
        }

        FetchStatus = FetchStatus.Paused;
        OnChange?.Invoke();
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
                        _ = ExecuteAsync(isRefetch: true);
                    }
                }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
    }

    private void StartRefetchInterval()
    {
        if (!_queryOptions.RefetchInterval.HasValue) return;

        _refetchIntervalCts = new CancellationTokenSource();
        var interval = _queryOptions.RefetchInterval.Value;

        _ = Task.Run(async () =>
        {
            while (!_refetchIntervalCts.IsCancellationRequested)
            {
                await Task.Delay(interval, _refetchIntervalCts.Token);
                if (_refetchIntervalCts.IsCancellationRequested) break;

                if (_onlineManager.IsOnline && FetchStatus != FetchStatus.Fetching)
                {
                    _ = ExecuteAsync(isRefetch: Data != null);
                }
            }
        }, _refetchIntervalCts.Token);
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

        _staleTimerCts?.Cancel();
        _staleTimerCts?.Dispose();

        _refetchIntervalCts?.Cancel();
        _refetchIntervalCts?.Dispose();

        _fetchLock.Dispose();
    }

}
