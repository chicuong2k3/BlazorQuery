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
    
    public bool IsPending => Status == QueryStatus.Pending;
    public bool IsSuccess => Status == QueryStatus.Success;
    public bool IsError => Status == QueryStatus.Error;
    public bool IsFetching => FetchStatus == FetchStatus.Fetching;
    public bool IsPaused => FetchStatus == FetchStatus.Paused;

    /// <summary>
    /// Indicates if the current data is placeholder data (not persisted to cache).
    /// True when displaying placeholderData while fetching actual data.
    /// </summary>
    public bool IsPlaceholderData { get; private set; }

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
    private readonly Action<bool> _focusChangedHandler;
    private CancellationTokenSource? _staleTimerCts;
    private CancellationTokenSource? _refetchIntervalCts;
    
    /// <summary>
    /// Used to pause/resume retry when network goes offline/online during fetch
    /// </summary>
    private readonly SemaphoreSlim _pauseRetrySemaphore = new(0, 1);

    // Track previous state for placeholder data function
    private T? _previousData;
    private QueryOptions<T>? _previousQueryOptions;


    public UseQuery(
            QueryOptions<T> queryOptions,
            QueryClient client)
    {
        _queryOptions = queryOptions;
        _client = client;
        _onlineManager = client.OnlineManager;
        FetchStatus = FetchStatus.Idle;

        // Resolve QueryFn: use provided queryFn or fallback to default
        if (_queryOptions.QueryFn == null)
        {
            if (_client.DefaultQueryFn == null)
            {
                throw new InvalidOperationException(
                    "No queryFn provided and no default query function configured. " +
                    "Either provide a queryFn in QueryOptions or set QueryClient.DefaultQueryFn."
                );
            }
            
            // Wrap default query function with type casting
            _queryOptions = new QueryOptions<T>(
                queryKey: _queryOptions.QueryKey,
                queryFn: async ctx => {
                    var result = await _client.DefaultQueryFn(ctx);
                    if (result is T typedResult)
                        return typedResult;
                    throw new InvalidCastException(
                        $"Default query function returned {result?.GetType().Name ?? "null"} " +
                        $"but expected {typeof(T).Name}"
                    );
                },
                staleTime: _queryOptions.StaleTime,
                networkMode: _queryOptions.NetworkMode,
                refetchOnReconnect: _queryOptions.RefetchOnReconnect,
                retry: _queryOptions.Retry,
                retryInfinite: _queryOptions.RetryInfinite,
                retryFunc: _queryOptions.RetryFunc,
                retryDelay: _queryOptions.RetryDelay,
                maxRetryDelay: _queryOptions.MaxRetryDelay,
                retryDelayFunc: _queryOptions.RetryDelayFunc,
                refetchInterval: _queryOptions.RefetchInterval,
                meta: _queryOptions.Meta,
                enabled: _queryOptions.Enabled,
                refetchOnWindowFocus: _queryOptions.RefetchOnWindowFocus,
                initialData: _queryOptions.InitialData,
                initialDataFunc: _queryOptions.InitialDataFunc,
                initialDataUpdatedAt: _queryOptions.InitialDataUpdatedAt,
                placeholderData: _queryOptions.PlaceholderData,
                placeholderDataFunc: _queryOptions.PlaceholderDataFunc
            );
        }

        if (_queryOptions.NetworkMode == default)
            _queryOptions.NetworkMode = _client.DefaultNetworkMode;

        if (_queryOptions.NetworkMode == NetworkMode.Always)
            _queryOptions.RefetchOnReconnect = false;

        _onlineStatusHandler = () => _ = SafeHandleOnlineStatusChangedAsync();

        if (_queryOptions.RefetchOnReconnect)
        {
            _onlineManager.OnlineStatusChanged += _onlineStatusHandler;
        }

        // Subscribe to focus changes for refetchOnWindowFocus
        _focusChangedHandler = async void (isFocused) =>
        {
            try
            {
                await HandleFocusChangedAsync(isFocused);
            }
            catch (Exception)
            {
                // ignore
            }
        };
        
        if (_queryOptions.RefetchOnWindowFocus)
        {
            _client.FocusManager.FocusChanged += _focusChangedHandler;
        }

        // Subscribe to query invalidation events
        _client.OnQueriesInvalidated += HandleQueriesInvalidated;
        
        // Subscribe to query cancellation events
        _client.OnQueriesCancelled += HandleQueriesCancelled;

        // Handle initial data and placeholder data
        InitializeWithData();

        // Start interval polling if configured
        if (_queryOptions.RefetchInterval.HasValue)
            StartRefetchInterval();
    }

    private void HandleQueriesInvalidated(List<QueryKey> invalidatedKeys)
    {
        // Check if this query was invalidated
        if (!invalidatedKeys.Contains(_queryOptions.QueryKey)) return;
        // Refetch if this query is enabled
        if (_queryOptions.Enabled)
        {
            _ = ExecuteAsync();
        }
    }

    private void HandleQueriesCancelled(List<QueryKey> cancelledKeys)
    {
        // Check if this query should be cancelled
        if (cancelledKeys.Contains(_queryOptions.QueryKey))
        {
            // Cancel the current fetch
            _currentCts?.Cancel();
        }
    }

    private void InitializeWithData()
    {
        // Priority 1: Initial data (persisted to cache)
        T? initialData = default;
        bool hasInitialData = false;

        if (_queryOptions.InitialDataFunc != null)
        {
            // Lazy evaluation - only called once
            initialData = _queryOptions.InitialDataFunc();
            hasInitialData = initialData != null;
        }
        else if (_queryOptions.InitialData != null)
        {
            initialData = _queryOptions.InitialData;
            hasInitialData = true;
        }

        if (hasInitialData && initialData != null)
        {
            // Set initial data in cache (PERSISTED)
            _client.Set(_queryOptions.QueryKey, initialData);
            
            // Update cache entry timestamp if initialDataUpdatedAt was provided
            var entry = _client.GetCacheEntry(_queryOptions.QueryKey);
            if (entry != null && _queryOptions.InitialDataUpdatedAt.HasValue)
            {
                entry.FetchTime = _queryOptions.InitialDataUpdatedAt.Value;
            }

            // Set local state
            Data = initialData;
            IsPlaceholderData = false;
            
            // Note: Staleness checking happens in ExecuteAsync
            // If (UtcNow - entry.FetchTime) > staleTime: will refetch
            // If (UtcNow - entry.FetchTime) <= staleTime: won't refetch (still fresh)
            return;
        }

        // Priority 2: Placeholder data (NOT persisted to cache)
        T? placeholderData = default;
        bool hasPlaceholderData = false;

        if (_queryOptions.PlaceholderDataFunc != null)
        {
            // Function receives previousData and previousQuery for transitions
            placeholderData = _queryOptions.PlaceholderDataFunc(_previousData, _previousQueryOptions);
            hasPlaceholderData = placeholderData != null;
        }
        else if (_queryOptions.PlaceholderData != null)
        {
            placeholderData = _queryOptions.PlaceholderData;
            hasPlaceholderData = true;
        }

        if (hasPlaceholderData && placeholderData != null)
        {
            // Set placeholder data (NOT persisted to cache)
            Data = placeholderData;
            IsPlaceholderData = true;
            
            // Note: Query will still fetch actual data in background
            // When real data arrives, IsPlaceholderData will become false
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

    private async Task HandleFocusChangedAsync(bool isFocused)
    {
        // Only refetch when window gains focus (not when losing focus)
        if (!isFocused)
            return;

        // Don't refetch if query is disabled
        if (!_queryOptions.Enabled)
            return;

        // Don't refetch if already fetching
        if (FetchStatus == FetchStatus.Fetching)
            return;

        // Check if data is stale
        var entry = _client.GetCacheEntry(_queryOptions.QueryKey);
        var isDataStale = entry == null || (_queryOptions.StaleTime > TimeSpan.Zero &&
                                        (DateTime.UtcNow - entry.FetchTime) > _queryOptions.StaleTime);

        // Only refetch if data is stale
        if (isDataStale)
        {
            await ExecuteAsync();
        }
    }

    /// <summary>
    /// Executes the query manually.
    /// </summary>
    public async Task ExecuteAsync(CancellationToken? signal = null, bool isRefetch = false)
    {
        // If query is disabled, don't execute
        if (!_queryOptions.Enabled)
        {
            FetchStatus = FetchStatus.Idle;
            return;
        }

        await _fetchLock.WaitAsync();
        CancellationTokenSource? linkedCts = null;

        try
        {
            _staleTimerCts?.Cancel();

            // Cancel _currentCts only if a fetch is already running
            if (_currentCts != null && FetchStatus == FetchStatus.Fetching)
            {
                await _currentCts.CancelAsync();
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
            if (entry is { Data: T cached })
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
            _client.IncrementFetchingQueries(); // Track global fetching state

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
                    var fetchedData = await _client.FetchAsync(_queryOptions.QueryKey,
                                                    _ => _queryOptions.QueryFn!(ctx),
                                                    _queryOptions.StaleTime,
                                                    token);

                    // Save previous state before updating
                    _previousData = Data;
                    _previousQueryOptions = _queryOptions;
                    
                    // Set real data and clear placeholder flag
                    Data = fetchedData;
                    IsPlaceholderData = false;

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
                    // Only pause if we're offline (cancellation was due to going offline)
                    // If still online, this is a user cancellation - rethrow
                    if (_queryOptions.NetworkMode != NetworkMode.Always && !_onlineManager.IsOnline)
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
                        if (signal is { IsCancellationRequested: true })
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
                _client.DecrementFetchingQueries(); // Track global fetching state
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
        if (_staleTimerCts is { IsCancellationRequested: false })
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
            
            if (_queryOptions.RefetchOnWindowFocus)
                _client.FocusManager.FocusChanged -= _focusChangedHandler;
            
            // Unsubscribe from invalidation events
            _client.OnQueriesInvalidated -= HandleQueriesInvalidated;
            
            // Unsubscribe from cancellation events
            _client.OnQueriesCancelled -= HandleQueriesCancelled;
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
