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

    public QueryStatus Status
    {
        get
        {
            if (Error != null)
                return QueryStatus.Error;
            if (Data == null)
                return QueryStatus.Pending;
            return QueryStatus.Success;
        }
    }

    public bool IsFetchingBackground { get; private set; }
    public bool IsLoading => Status == QueryStatus.Pending && 
                             (FetchStatus == FetchStatus.Fetching || FetchStatus == FetchStatus.Paused);

    public int FailureCount { get; private set; }
    public bool IsRefetchError { get; private set; }

    /// <summary>
    /// The error from the most recent retry attempt.
    /// Available during retry attempts before the final error is set.
    /// After the last retry fails, this becomes the Error property.
    /// </summary>
    public Exception? FailureReason => _lastError;

    public event Action? OnChange;
    private readonly Action _onlineStatusHandler;
    private CancellationTokenSource? _staleTimerCts;
    private CancellationTokenSource? _refetchIntervalCts;
    
    /// <summary>
    /// Used to pause/resume retry when network goes offline/online during fetch
    /// </summary>
    private readonly SemaphoreSlim _pauseRetrySemaphore = new(0, 1);


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

        if (_queryOptions.NetworkMode != NetworkMode.Always || _queryOptions.RefetchOnReconnect)
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
        // Handle going offline - pause any in-progress fetch
        if (!_onlineManager.IsOnline)
        {
            if (_queryOptions.NetworkMode != NetworkMode.Always && FetchStatus == FetchStatus.Fetching)
            {
                // Cancel the current fetch - this will trigger OperationCanceledException
                // which sets FetchStatus to Paused (see catch block in ExecuteAsync)
                _currentCts?.Cancel();
            }
            return Task.CompletedTask;
        }

        // If query is paused (mid-retry), signal to continue
        // This is different from refetchOnReconnect - this continues existing fetch
        if (FetchStatus == FetchStatus.Paused)
        {
            // Release the semaphore to continue the paused retry
            bool wasSomeoneWaiting = false;
            try
            {
                if (_pauseRetrySemaphore.CurrentCount == 0)
                {
                    _pauseRetrySemaphore.Release();
                    // If count is still 0 after release, someone consumed it (was waiting)
                    wasSomeoneWaiting = _pauseRetrySemaphore.CurrentCount == 0;
                }
            }
            catch (SemaphoreFullException)
            {
                // Already released, ignore
            }

            // If someone was waiting on the semaphore, they will continue the fetch
            if (wasSomeoneWaiting)
                return Task.CompletedTask;

            // No one was waiting - this means we paused before actually starting
            // Fall through to refetch logic below
        }

        // if there's already a fetch in progress, don't start another
        if (FetchStatus == FetchStatus.Fetching)
            return Task.CompletedTask;

        // This is refetchOnReconnect behavior - start new fetch for stale data
        // Applies to NetworkMode.Online and NetworkMode.OfflineFirst when RefetchOnReconnect is enabled
        if (_queryOptions.RefetchOnReconnect && _queryOptions.NetworkMode != NetworkMode.Always)
        {
            var entry = _client.GetCacheEntry(_queryOptions.QueryKey);
            var isDataStale = entry == null || (_queryOptions.StaleTime > TimeSpan.Zero &&
                                            (DateTime.UtcNow - entry.FetchTime) > _queryOptions.StaleTime);

            if (isDataStale)
            {
                _ = ExecuteAsync();
            }
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

            TimeSpan maxRetryDelay = _queryOptions.MaxRetryDelay ?? TimeSpan.FromSeconds(30);

            // attemptIndex tracks retry attempts (0 = first retry, 1 = second retry, etc.)
            int attemptIndex = -1; // -1 means initial attempt (not a retry yet)
            
            for (;;)
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
                    attemptIndex++; // Increment for each failure (0 = first retry)
                    
                    bool shouldRetry = false;

                    // Retry logic:
                    // - retry: false = no retries
                    // - retry: true = infinite retries  
                    // - retry: 6 = retry 6 times after initial (7 total attempts)
                    // - retry: (failureCount, error) => custom logic (failureCount starts at 0)
                    
                    if (_queryOptions.RetryInfinite) 
                        shouldRetry = true;
                    else if (_queryOptions.Retry.HasValue && attemptIndex < _queryOptions.Retry.Value) 
                        shouldRetry = true;
                    else if (_queryOptions.RetryFunc != null) 
                        shouldRetry = _queryOptions.RetryFunc(attemptIndex, ex);

                    if (!shouldRetry)
                    {
                        Error = _lastError;
                        if (isRefetch) IsRefetchError = true;
                        break;
                    }

                    // pause retry if offline, wait for online, then continue
                    // This is not a refetch - it continues from current attempt
                    if (_queryOptions.NetworkMode != NetworkMode.Always && !_onlineManager.IsOnline)
                    {
                        FetchStatus = FetchStatus.Paused;
                        Notify();
                        
                        // Wait for network to come back online
                        // The semaphore will be released by OnOnlineStatusChangedAsync
                        await _pauseRetrySemaphore.WaitAsync(token);
                        
                        // Check if still online and not cancelled
                        if (!_onlineManager.IsOnline || token.IsCancellationRequested)
                        {
                            FetchStatus = FetchStatus.Paused;
                            return;
                        }
                        
                        // Resume - set back to Fetching and continue retry loop
                        FetchStatus = FetchStatus.Fetching;
                    }

                    int delayMs; 
                    if (_queryOptions.RetryDelayFunc != null)
                    {
                        delayMs = (int)_queryOptions.RetryDelayFunc(attemptIndex).TotalMilliseconds;
                    }
                    else
                    {
                        // Default: Math.min(1000 * 2^attemptIndex, 30000)
                        double expDelay = 1000 * Math.Pow(2, attemptIndex);
                        delayMs = (int)Math.Min(expDelay, maxRetryDelay.TotalMilliseconds);
                    }

                    try
                    {
                        await Task.Delay(delayMs, token);
                    }
                    catch (OperationCanceledException) when (_queryOptions.NetworkMode != NetworkMode.Always && !_onlineManager.IsOnline)
                    {
                        // Delay was cancelled because we went offline - pause and wait for reconnect
                        FetchStatus = FetchStatus.Paused;
                        Notify();

                        // Wait without token since we were cancelled for going offline, not user cancellation
                        // If disposed, semaphore.Dispose() will interrupt this wait
                        try
                        {
                            await _pauseRetrySemaphore.WaitAsync();
                        }
                        catch (ObjectDisposedException)
                        {
                            // Query was disposed while paused
                            return;
                        }

                        // Check if user cancelled while we were paused
                        if (signal.HasValue && signal.Value.IsCancellationRequested)
                        {
                            FetchStatus = FetchStatus.Paused;
                            return;
                        }

                        if (!_onlineManager.IsOnline)
                        {
                            FetchStatus = FetchStatus.Paused;
                            return;
                        }

                        FetchStatus = FetchStatus.Fetching;
                        continue; // Continue retry loop after resume
                    }

                    // Check if we went offline during the delay (without cancellation)
                    if (_queryOptions.NetworkMode != NetworkMode.Always && !_onlineManager.IsOnline)
                    {
                        FetchStatus = FetchStatus.Paused;
                        Notify();

                        await _pauseRetrySemaphore.WaitAsync(token);

                        if (!_onlineManager.IsOnline || token.IsCancellationRequested)
                        {
                            FetchStatus = FetchStatus.Paused;
                            return;
                        }

                        FetchStatus = FetchStatus.Fetching;
                    }
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
            catch
            {
                // ignored
            }
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
            if (_queryOptions.NetworkMode != NetworkMode.Always || _queryOptions.RefetchOnReconnect)
                _onlineManager.OnlineStatusChanged -= _onlineStatusHandler;
        }
        catch
        {
            // ignored
        }

        _currentCts?.Cancel();
        _currentCts?.Dispose();

        _staleTimerCts?.Cancel();
        _staleTimerCts?.Dispose();

        _refetchIntervalCts?.Cancel();
        _refetchIntervalCts?.Dispose();

        _fetchLock.Dispose();
        _pauseRetrySemaphore.Dispose();
    }

}
