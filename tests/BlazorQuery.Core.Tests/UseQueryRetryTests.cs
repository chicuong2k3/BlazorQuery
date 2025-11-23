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
            _ => { 
                count++; 
                if (count < 2) 
                    throw new Exception("Fail"); 
                
                return Task.FromResult(new List<string> { "ok" }); 
            }, 
            retry: 1
        ); 
        
        SetOnline(true); 
        await query.ExecuteAsync(); 
        await query.RefetchAsync(); 
        Assert.True(query.IsRefetchError); 
        Assert.NotNull(query.Error); 
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
    
    //[Fact] 
    //public async Task Retry_ShouldPauseWhenOffline() 
    //{ 
    //    int count = 0; 
    //    var query = CreateQuery(
    //        NetworkMode.Online, 
    //        async _ => { 
    //            count++; 
    //            return new List<string> { "ok" }; 
    //        }, 
    //        retry: 3
    //    ); 
        
    //    SetOnline(false); 
    //    var task = query.ExecuteAsync(); 
    //    await Task.Delay(20); 
        
    //    Assert.Equal(FetchStatus.Paused, query.FetchStatus);

    //    var tcs = new TaskCompletionSource();
    //    query.OnChange += () =>
    //    {
    //        if (query.Status == QueryStatus.Success)
    //            tcs.TrySetResult();
    //    };

    //    SetOnline(true);
    //    await tcs.Task;
    //    Assert.Equal(QueryStatus.Success, query.Status); 
    //}
    
}
