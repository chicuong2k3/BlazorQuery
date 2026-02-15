namespace SwrSharp.Core;

/// <summary>
/// Manages a mutation (create/update/delete) operation with lifecycle callbacks, retry, and scope support.
/// </summary>
public class UseMutation<TData, TVariables> : IDisposable
{
    private readonly MutationOptions<TData, TVariables> _options;
    private readonly QueryClient _client;
    private readonly MutationContext _context;
    private readonly IOnlineManager _onlineManager;
    private long _mutationId;

    public TData? Data { get; private set; }
    public Exception? Error { get; private set; }
    public TVariables? Variables { get; private set; }
    public MutationStatus Status { get; private set; } = MutationStatus.Idle;
    public bool IsIdle => Status == MutationStatus.Idle;
    public bool IsPending => Status == MutationStatus.Pending;
    public bool IsError => Status == MutationStatus.Error;
    public bool IsSuccess => Status == MutationStatus.Success;
    public bool IsPaused { get; private set; }
    public int FailureCount { get; private set; }
    public Exception? FailureReason { get; private set; }
    public DateTime? SubmittedAt { get; private set; }

    public event Action? OnChange;

    public UseMutation(MutationOptions<TData, TVariables> options, QueryClient client)
    {
        _options = options;
        _client = client;
        _context = new MutationContext(client);
        _onlineManager = client.OnlineManager;
    }

    /// <summary>
    /// Fire-and-forget mutation. Errors are swallowed (use callbacks for error handling).
    /// </summary>
    public void Mutate(TVariables variables, MutateOptions<TData, TVariables>? options = null)
    {
        _ = MutateInternalAsync(variables, options, throwOnError: false);
    }

    /// <summary>
    /// Awaitable mutation that returns data on success or throws on error.
    /// </summary>
    public async Task<TData> MutateAsync(TVariables variables, MutateOptions<TData, TVariables>? options = null)
    {
        return await MutateInternalAsync(variables, options, throwOnError: true);
    }

    /// <summary>
    /// Reset mutation state back to idle.
    /// </summary>
    public void Reset()
    {
        Data = default;
        Error = null;
        Variables = default;
        Status = MutationStatus.Idle;
        IsPaused = false;
        FailureCount = 0;
        FailureReason = null;
        SubmittedAt = null;
        OnChange?.Invoke();
    }

    private async Task<TData> MutateInternalAsync(
        TVariables variables,
        MutateOptions<TData, TVariables>? mutateOptions,
        bool throwOnError)
    {
        var currentMutationId = Interlocked.Increment(ref _mutationId);

        Variables = variables;
        Status = MutationStatus.Pending;
        Error = null;
        Data = default;
        FailureCount = 0;
        FailureReason = null;
        SubmittedAt = DateTime.UtcNow;
        IsPaused = false;
        OnChange?.Invoke();

        // Check network mode - pause if offline
        if (_options.NetworkMode == NetworkMode.Online && !_onlineManager.IsOnline)
        {
            IsPaused = true;
            OnChange?.Invoke();
            // For now, throw - caller can retry when online
            throw new InvalidOperationException("Cannot mutate while offline with NetworkMode.Online");
        }

        object? onMutateResult = null;

        try
        {
            // OnMutate callback (option-level)
            if (_options.OnMutate != null)
            {
                onMutateResult = await _options.OnMutate(variables, _context);
            }

            // Execute with scope serialization if configured
            TData result;
            if (_options.Scope != null)
            {
                var semaphore = _client.GetScopeSemaphore(_options.Scope.Id);
                await semaphore.WaitAsync();
                try
                {
                    result = await ExecuteWithRetryAsync(variables);
                }
                finally
                {
                    semaphore.Release();
                }
            }
            else
            {
                result = await ExecuteWithRetryAsync(variables);
            }

            Data = result;
            Status = MutationStatus.Success;
            FailureCount = 0;
            FailureReason = null;

            // OnSuccess callback (option-level first)
            if (_options.OnSuccess != null)
            {
                await _options.OnSuccess(result, variables, onMutateResult, _context);
            }

            // Per-call OnSuccess (only fires for the LAST mutate call)
            if (mutateOptions?.OnSuccess != null && currentMutationId == Interlocked.Read(ref _mutationId))
            {
                await mutateOptions.OnSuccess(result, variables, onMutateResult, _context);
            }

            // OnSettled callback (option-level first)
            if (_options.OnSettled != null)
            {
                await _options.OnSettled(result, null, variables, onMutateResult, _context);
            }

            // Per-call OnSettled (only fires for the LAST mutate call)
            if (mutateOptions?.OnSettled != null && currentMutationId == Interlocked.Read(ref _mutationId))
            {
                await mutateOptions.OnSettled(result, null, variables, onMutateResult, _context);
            }

            OnChange?.Invoke();
            return result;
        }
        catch (Exception ex)
        {
            Error = ex;
            Status = MutationStatus.Error;

            // OnError callback (option-level first)
            if (_options.OnError != null)
            {
                await _options.OnError(ex, variables, onMutateResult, _context);
            }

            // Per-call OnError (only fires for the LAST mutate call)
            if (mutateOptions?.OnError != null && currentMutationId == Interlocked.Read(ref _mutationId))
            {
                await mutateOptions.OnError(ex, variables, onMutateResult, _context);
            }

            // OnSettled callback (option-level first)
            if (_options.OnSettled != null)
            {
                await _options.OnSettled(default, ex, variables, onMutateResult, _context);
            }

            // Per-call OnSettled (only fires for the LAST mutate call)
            if (mutateOptions?.OnSettled != null && currentMutationId == Interlocked.Read(ref _mutationId))
            {
                await mutateOptions.OnSettled(default, ex, variables, onMutateResult, _context);
            }

            OnChange?.Invoke();

            if (throwOnError)
                throw;

            return default!;
        }
    }

    private async Task<TData> ExecuteWithRetryAsync(TVariables variables)
    {
        var maxRetryDelay = _options.MaxRetryDelay ?? TimeSpan.FromSeconds(30);
        int attemptIndex = 0;

        for (;;)
        {
            try
            {
                return await _options.MutationFn(variables);
            }
            catch (Exception ex)
            {
                FailureCount++;
                FailureReason = ex;

                bool shouldRetry = attemptIndex < _options.Retry;

                if (!shouldRetry)
                    throw;

                // Compute delay
                int delayMs;
                if (_options.RetryDelayFunc != null)
                {
                    delayMs = (int)_options.RetryDelayFunc(attemptIndex).TotalMilliseconds;
                }
                else if (_options.RetryDelay.HasValue)
                {
                    delayMs = (int)_options.RetryDelay.Value.TotalMilliseconds;
                }
                else
                {
                    double expDelay = 1000 * Math.Pow(2, attemptIndex);
                    delayMs = (int)Math.Min(expDelay, maxRetryDelay.TotalMilliseconds);
                }

                attemptIndex++;

                if (delayMs > 0)
                    await Task.Delay(delayMs);
            }
        }
    }

    public void Dispose()
    {
        // No persistent subscriptions to clean up
    }
}
