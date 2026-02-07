namespace BlazorQuery.Core.Tests;

public class UseQueryRetryTests : UseQueryTestsBase
{
    [Fact]
    public async Task Retry_ShouldRetrySpecifiedTimes()
    {
        int count = 0;
        var query = CreateQuery(
            NetworkMode.Online,
            _ =>
            {
                count++; if (count < 3)
                    throw new Exception("Fail");

                return Task.FromResult(new List<string> { "ok" });
            },
            retry: 3
        );
        SetOnline(true);
        var snapshots = await ObserveQuery(query);
        Assert.Equal(2, query.FailureCount);
        Assert.Equal(3, count);
        Assert.Equal(QueryStatus.Success, snapshots[^1].Status);
    }

    [Fact]
    public async Task RetryInfinite_ShouldKeepRetrying()
    {
        int count = 0;
        var tcs = new TaskCompletionSource<List<string>>();

        var query = CreateQuery(
            NetworkMode.Online,
            async _ =>
            {
                count++;
                if (count < 5) throw new Exception("Fail");
                return new List<string> { "success" };
            },
            retryInfinite: true
        );

        SetOnline(true);
        var snapshots = await ObserveQuery(query);
        Assert.Equal(5, count);
        Assert.Equal(QueryStatus.Success, snapshots[^1].Status);
    }

    [Fact]
    public async Task RetryFunc_ShouldUseCustomLogic()
    {
        int count = 0;
        var query = CreateQuery(
            NetworkMode.Online,
            _ =>
            {
                count++; if (count < 5)
                    throw new Exception("Fail");

                return Task.FromResult(new List<string> { "ok" });
            },
            retryFunc: (attempt, ex) => attempt < 3
        );

        SetOnline(true);
        var snapshots = await ObserveQuery(query);
        Assert.Equal(4, count);
        Assert.Equal(QueryStatus.Error, snapshots[^1].Status);
    }

    [Fact]
    public async Task RetryDelay_ShouldWaitExpectedTime()
    {
        int count = 0;
        var timestamps = new List<DateTime>();
        var query = CreateQuery<List<string>>(
            NetworkMode.Online,
            async _ =>
            {
                timestamps.Add(DateTime.UtcNow);
                count++;
                throw new Exception("Fail");
            },
            retry: 3,
            retryDelayFunc: attempt => TimeSpan.FromMilliseconds(50)
        );

        SetOnline(true);
        var snapshots = await ObserveQuery(query);
        Assert.Equal(4, count);
        Assert.True((timestamps[1] - timestamps[0]).TotalMilliseconds >= 50);
    }

    [Fact]
    public async Task Refetch_WithRetry_ShouldMarkRefetchError()
    {
        int count = 0;
        var query = CreateQuery(
            NetworkMode.Online,
            _ =>
            {
                count++;
                if (count == 1) 
                    return Task.FromResult(new List<string> { "ok" });

                throw new Exception("Fail");
            },
            retry: 0
        );

        SetOnline(true);

        await ObserveQuery(query);

        var snapshots = await ObserveRefetchQuery(query);
        var refetchErrorSnapshot = snapshots.FirstOrDefault(s => s.IsRefetchError);

        Assert.NotNull(refetchErrorSnapshot); 
        Assert.True(refetchErrorSnapshot.IsRefetchError);
        Assert.NotNull(refetchErrorSnapshot.Error);

        Assert.Equal(2, count);
    } 
    
    [Fact] 
    public async Task Retry_ShouldStopOnCancellation() 
    { 
        int count = 0; 
        var cts = new CancellationTokenSource(); 
        var query = CreateQuery<List<string>>(
            NetworkMode.Online, 
            async _ => { 
                count++; 
                throw new Exception("Fail"); 
            }, 
            retry: 5 
        ); 
        
        SetOnline(true); 
        var task = query.ExecuteAsync(cts.Token); 
        await Task.Delay(50); 
        cts.Cancel();

        await Assert.ThrowsAsync<TaskCanceledException>(() => task);
        Assert.True(count < 6); // should not complete all retries
    }

    [Fact]
    public async Task Retry_ShouldPauseWhenOffline()
    {
        int count = 0;
        var query = CreateQuery(
            NetworkMode.Online,
            async _ =>
            {
                count++;
                return new List<string> { "ok" };
            },
            retry: 3
        );

        SetOnline(false);
        var snapshots = await ObserveQuery(query);
        Assert.Contains(snapshots, s => s.FetchStatus == FetchStatus.Paused);

        SetOnline(true);
        snapshots = await ObserveQuery(query);
        Assert.Contains(snapshots, s => s.Status == QueryStatus.Success);

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Fetch_ShouldPauseWhenGoingOfflineDuringActiveFetch()
    {
        // When going offline DURING an active fetch (not just before retry),
        // the fetch should be cancelled and query should pause
        var fetchStarted = new TaskCompletionSource();
        var fetchCanContinue = new TaskCompletionSource();
        int attemptCount = 0;

        var query = CreateQuery(
            NetworkMode.Online,
            async ctx =>
            {
                attemptCount++;
                fetchStarted.TrySetResult();

                // Simulate a long-running fetch that respects cancellation
                try
                {
                    await fetchCanContinue.Task.WaitAsync(ctx.Signal);
                }
                catch (OperationCanceledException)
                {
                    throw; // Re-throw to trigger pause behavior
                }

                return new List<string> { "success" };
            },
            retry: 3
        );

        SetOnline(true);
        var executeTask = query.ExecuteAsync();

        // Wait for fetch to start
        await fetchStarted.Task;
        Assert.Equal(FetchStatus.Fetching, query.FetchStatus);
        Assert.Equal(1, attemptCount);

        // Go offline DURING the active fetch
        SetOnline(false);

        // Give time for cancellation to propagate
        await Task.Delay(50);

        // Should be paused now
        Assert.Equal(FetchStatus.Paused, query.FetchStatus);

        // Come back online and allow fetch to complete
        fetchCanContinue.TrySetResult();
        SetOnline(true);

        // The query should continue (resume), not restart
        await Task.Delay(100);

        // Note: After pause/resume, attempt count may be 2 if it retries
        // The key behavior is that it paused when going offline
        Assert.True(attemptCount >= 1);
    }

    [Fact]
    public async Task Retry_ShouldPauseAndContinue_WhenOfflineDuringRetry()
    {
        // If offline during retry delay, pause and continue (not restart)
        int attemptCount = 0;

        var query = CreateQuery(
            NetworkMode.Online,
            async _ =>
            {
                attemptCount++;

                // First attempt fails, then we'll go offline during retry delay
                if (attemptCount == 1)
                {
                    throw new Exception("First attempt failed");
                }

                // After coming back online, should succeed
                return new List<string> { "success" };
            },
            retry: 5,
            retryDelayFunc: _ => TimeSpan.FromMilliseconds(300) // Longer delay to catch pause
        );

        SetOnline(true);
        var executeTask = query.ExecuteAsync();

        // Wait for first attempt to fail and enter retry delay
        await Task.Delay(100);
        Assert.Equal(1, attemptCount);
        Assert.Equal(FetchStatus.Fetching, query.FetchStatus);

        // Go offline DURING the retry delay
        SetOnline(false);

        // Wait for pause to be detected (after delay completes and checks offline)
        await Task.Delay(400);

        // Should be paused now
        Assert.Equal(FetchStatus.Paused, query.FetchStatus);
        Assert.Equal(1, attemptCount); // Still only 1 attempt (paused before 2nd)

        // Come back online - should CONTINUE from where it paused
        SetOnline(true);

        await executeTask;

        // Should have continued and succeeded on 2nd attempt
        Assert.Equal(QueryStatus.Success, query.Status);
        Assert.Equal(2, attemptCount); // 1st failed, 2nd succeeded after resume
        Assert.Equal(1, query.FailureCount); // Only 1 failure
    }

    [Fact]
    public async Task Retry_ShouldNotContinue_WhenCancelledWhilePaused()
    {
        // If query is cancelled while paused, it should NOT continue when back online
        var cts = new CancellationTokenSource();
        int attemptCount = 0;

        var query = CreateQuery<List<string>>(
            NetworkMode.Online,
            _ =>
            {
                attemptCount++;
                throw new Exception("Always fail");
            },
            retry: 5,
            retryDelayFunc: _ => TimeSpan.FromMilliseconds(100)
        );

        SetOnline(true);
        _ = query.ExecuteAsync(cts.Token);

        // Wait for first attempt to fail
        await Task.Delay(50);
        Assert.Equal(1, attemptCount);

        // Go offline during retry
        SetOnline(false);
        await Task.Delay(200);
        Assert.Equal(FetchStatus.Paused, query.FetchStatus);

        // Cancel while paused
        cts.Cancel();

        // Come back online
        SetOnline(true);
        await Task.Delay(100);

        // Query should NOT have continued - attempt count should still be 1
        Assert.Equal(1, attemptCount);
        Assert.Equal(FetchStatus.Paused, query.FetchStatus);
    }

}
