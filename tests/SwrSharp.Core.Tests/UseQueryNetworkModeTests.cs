using SwrSharp.Core.Tests.Helpers;

namespace SwrSharp.Core.Tests;

public class UseQueryNetworkModeTests : UseQueryTestsBase
{
    public UseQueryNetworkModeTests()
    {
    }

    [Fact]
    public async Task OnlineMode_OnlineNetwork_ShouldFetchSuccess()
    {
        SetOnline(true);
        var query = CreateQuery(NetworkMode.Online, _ => FakeNetworkApi());
        var snapshots = await ObserveQuery(query);
        var final = snapshots.Last();
        Assert.Equal(QueryStatus.Success, final.Status);
        Assert.False(final.IsLoading);
        Assert.NotNull(final.Data);
        Assert.Equal(FetchStatus.Idle, final.FetchStatus);
        Assert.Contains(snapshots, s => s.FetchStatus == FetchStatus.Fetching || s.IsLoading);
    }

    [Fact]
    public async Task OnlineMode_OfflineNetwork_NoCache_ShouldPause()
    {
        SetOnline(false);
        var query = CreateQuery(NetworkMode.Online, _ => FakeNetworkApi());
        var snapshots = await ObserveQuery(query);
        var final = snapshots.Last();
        Assert.Equal(QueryStatus.Pending, final.Status);
        Assert.Equal(FetchStatus.Paused, final.FetchStatus);
        Assert.Null(final.Data);
    }

    [Fact]
    public async Task OnlineMode_OfflineNetwork_WithFreshCache_ShouldReturnCache()
    {
        SeedCache(new List<string> { "cached" }, TimeSpan.FromMinutes(5));
        SetOnline(false);
        var query = CreateQuery(NetworkMode.Online, _ => FakeNetworkApi(), TimeSpan.FromMinutes(5));
        var snapshots = await ObserveQuery(query);
        var final = snapshots.Last();
        Assert.Equal(QueryStatus.Success, final.Status);
        Assert.False(final.IsLoading);
        Assert.Equal(FetchStatus.Paused, final.FetchStatus);
        Assert.NotNull(final.Data);
    }

    [Fact]
    public async Task AlwaysMode_OnlineNetwork_ShouldFetchSuccess()
    {
        SetOnline(true);
        var query = CreateQuery(NetworkMode.Always, _ => FakeNetworkApi());
        var snapshots = await ObserveQuery(query);
        var final = snapshots.Last();
        Assert.Equal(QueryStatus.Success, final.Status);
        Assert.False(final.IsLoading);
        Assert.NotNull(final.Data);
        Assert.Equal(FetchStatus.Idle, final.FetchStatus);
        Assert.Contains(snapshots, s => s.FetchStatus == FetchStatus.Fetching || s.IsLoading);
    }

    [Fact]
    public async Task AlwaysMode_OfflineNetwork_ShouldError()
    {
        SetOnline(false);
        var query = CreateQuery<List<string>>(NetworkMode.Always, _ =>
        {
            throw new InvalidOperationException("Offline");
        }, retry: 0);
        var snapshots = await ObserveQuery(query);
        var final = snapshots.Last();
        Assert.Equal(QueryStatus.Error, final.Status);
        Assert.NotNull(final.Error);
        Assert.Equal(FetchStatus.Idle, final.FetchStatus);
    }

    [Fact]
    public async Task OfflineFirstMode_OnlineNetwork_NoCache_ShouldFetchSuccess()
    {
        SetOnline(true);
        var query = CreateQuery(NetworkMode.OfflineFirst, _ => FakeNetworkApi());
        var snapshots = await ObserveQuery(query);
        var final = snapshots.Last();
        Assert.Equal(QueryStatus.Success, final.Status);
        Assert.False(final.IsLoading);
        Assert.NotNull(final.Data);
        Assert.Equal(FetchStatus.Idle, final.FetchStatus);
        Assert.Contains(snapshots, s => s.FetchStatus == FetchStatus.Fetching || s.IsLoading);
    }

    [Fact]
    public async Task OfflineFirstMode_OnlineNetwork_WithFreshCache_ShouldReturnCacheWithoutFetch()
    {
        SeedCache(new List<string> { "fresh" }, TimeSpan.FromMinutes(5));
        SetOnline(true);
        var query = CreateQuery(NetworkMode.OfflineFirst, _ => FakeNetworkApi(), TimeSpan.FromMinutes(5));
        var snapshots = await ObserveQuery(query);
        var final = snapshots.Last();
        Assert.Equal(QueryStatus.Success, final.Status);
        Assert.False(final.IsLoading);
        Assert.Contains("fresh", final.Data!);
        Assert.Equal(FetchStatus.Idle, final.FetchStatus);
        Assert.DoesNotContain(snapshots, s => s.FetchStatus == FetchStatus.Fetching); // No fetch should occur
    }

    [Fact]
    public async Task OfflineFirstMode_OfflineNetwork_NoCache_ShouldPause()
    {
        SetOnline(false);
        var query = CreateQuery(NetworkMode.OfflineFirst, _ => FakeNetworkApi());
        var snapshots = await ObserveQuery(query);
        var final = snapshots.Last();
        Assert.NotEqual(QueryStatus.Success, final.Status);
        Assert.True(final.FetchStatus == FetchStatus.Paused && final.Status == QueryStatus.Pending);
        Assert.Null(final.Data);
    }

    [Fact]
    public async Task OfflineFirstMode_OfflineNetwork_WithFreshCache_ShouldReturnCache()
    {
        SeedCache(new List<string> { "fresh" }, TimeSpan.FromMinutes(5));
        SetOnline(false);
        var query = CreateQuery(NetworkMode.OfflineFirst, _ => FakeNetworkApi(), TimeSpan.FromMinutes(5));
        var snapshots = await ObserveQuery(query);
        var final = snapshots.Last();
        Assert.Equal(QueryStatus.Success, final.Status);
        Assert.False(final.IsLoading);
        Assert.Contains("fresh", final.Data!);
        Assert.Equal(FetchStatus.Paused, final.FetchStatus);
    }

    [Fact]
    public async Task OfflineFirstMode_OfflineNetwork_StaleCache_ShouldPause()
    {
        SeedCache(new List<string> { "stale" }, TimeSpan.FromMilliseconds(1));
        await Task.Delay(10);
        SetOnline(false);
        var query = CreateQuery(NetworkMode.OfflineFirst, _ => FakeNetworkApi(), TimeSpan.FromMilliseconds(1));
        var snapshots = await ObserveQuery(query);
        var final = snapshots.Last();
        Assert.Equal(QueryStatus.Success, final.Status);
        Assert.False(final.IsLoading);
        Assert.Contains("stale", final.Data!);
        Assert.Equal(FetchStatus.Paused, final.FetchStatus);
    }

    [Fact]
    public async Task OfflineFirstMode_Reconnect_ShouldAutoRefetch()
    {
        SetOnline(false);
        var refetchCount = 0;
        var query = CreateQuery(NetworkMode.OfflineFirst, _ =>
        {
            refetchCount++;
            return FakeNetworkApi();
        }, TimeSpan.FromMilliseconds(500), refetchOnReconnect: true);
        using var observer = new QueryObserver<List<string>>(query);
        await query.ExecuteAsync();
        await Task.Delay(50);
        SetOnline(true);
        await Task.Delay(100);
        var snapshots = observer.Snapshots;
        var final = snapshots.Last();
        Assert.True(refetchCount > 0);
        Assert.Equal(QueryStatus.Success, final.Status);
        Assert.Contains("network", final.Data![0]);
    }

    [Fact]
    public async Task OnlineMode_OfflineMidFetch_ThenReconnect_ShouldRefetchFromStart()
    {
        var tcsFirstFetch = new TaskCompletionSource<List<string>>();
        var tcsSecondFetch = new TaskCompletionSource<List<string>>();
        var fetchCount = 0;

        var queryFn = new Func<QueryFunctionContext, Task<List<string>>>(ctx =>
        {
            fetchCount++;
            return (fetchCount == 1 ? tcsFirstFetch.Task : tcsSecondFetch.Task)
                    .WaitAsync(ctx.Signal);
        });

        SetOnline(true);

        var query = CreateQuery(
            NetworkMode.Online,
            queryFn,
            refetchOnReconnect: true,
            staleTime: TimeSpan.FromMinutes(1) 
        );

        using var observer = new QueryObserver<List<string>>(query);

        // Start the first fetch
        var fetchTask = query.ExecuteAsync();

        // Simulate going offline mid-fetch
        // SetOnline now raises the event which cancels the fetch
        SetOnline(false);

        // Swallow cancellation
        try { await fetchTask; } catch (OperationCanceledException) { }

        Assert.Equal(FetchStatus.Paused, query.FetchStatus);

        // Reconnect - SetOnline now raises the event automatically
        SetOnline(true);

        await Task.Yield();

        // Complete second fetch
        tcsSecondFetch.TrySetResult(new List<string> { "refetched" });

        // Wait for correct snapshot
        var final = await observer.WaitForNextSnapshotAsync(
            s => s.Data?.Contains("refetched") == true,
            timeoutMs: 10000);

        Assert.Equal(QueryStatus.Success, final.Status);
        Assert.NotNull(final.Data);
        Assert.Contains("refetched", final.Data);
        Assert.Equal(FetchStatus.Idle, final.FetchStatus);
        Assert.Equal(2, fetchCount);
    }

    [Fact]
    public async Task StaleTime_WhenDataBecomesStale_ShouldRefetchInBackground()
    {
        var fetchCount = 0;
        var query = CreateQuery(
            NetworkMode.Online,
            _ => { fetchCount++; return Task.FromResult(new List<string> { "data" }); },
            staleTime: TimeSpan.FromMilliseconds(100));

        SetOnline(true);
        await query.ExecuteAsync();
        Assert.Equal(1, fetchCount);

        await Task.Delay(200); // exceed staleTime

        Assert.Equal(2, fetchCount); // background refetch
        Assert.True(query.IsFetchingBackground);
    }

    //[Fact]
    //public async Task RefetchInterval_ShouldRefetchAtInterval()
    //{
    //    var fetchCount = 0;
    //    var query = CreateQuery(
    //        NetworkMode.Online,
    //        _ => { fetchCount++; return Task.FromResult(new List<string>()); },
    //        staleTime: TimeSpan.Zero,
    //        refetchInterval: TimeSpan.FromMilliseconds(100));

    //    SetOnline(true);
    //    await query.ExecuteAsync();

    //    await Task.Delay(350);
    //    Assert.True(fetchCount >= 3, $"Expected >=3 fetches, got {fetchCount}");
    //}

    //[Fact]
    //public async Task RefetchInterval_ShouldNotRefetchWhenOffline()
    //{
    //    int fetchCount = 0;

    //    var query = CreateQuery(
    //        NetworkMode.Online,
    //        async (ctx) =>
    //        {
    //            fetchCount++;
    //            await Task.Delay(10);
    //            return new List<string>();
    //        },
    //        TimeSpan.Zero,
    //        true,
    //        null,
    //        TimeSpan.FromMilliseconds(100)
    //    );

    //    SetOnline(false);
    //    await query.ExecuteAsync();

    //    await Task.Delay(350); // interval should skip fetches
    //    Assert.Equal(1, fetchCount); // only initial execute
    //}

    //[Fact]
    //public async Task RefetchInterval_ShouldResumeWhenBackOnline()
    //{
    //    int fetchCount = 0;

    //    var query = CreateQuery(
    //        NetworkMode.Online,
    //        async (ctx) =>
    //        {
    //            fetchCount++;
    //            await Task.Delay(10);
    //            return new List<string>();
    //        },
    //        TimeSpan.Zero,
    //        true,
    //        null,
    //        TimeSpan.FromMilliseconds(100)
    //    );

    //    // offline initially
    //    SetOnline(false);
    //    await query.ExecuteAsync();
    //    await Task.Delay(150);
    //    Assert.Equal(1, fetchCount);

    //    // back online
    //    SetOnline(true);
    //    _onlineManagerMock.Raise(m => m.OnlineStatusChanged += null);

    //    await Task.Delay(350);
    //    Assert.True(fetchCount >= 3, $"Expected >=3 fetches after reconnect, got {fetchCount}");
    //}

    //[Fact]
    //public async Task RefetchInterval_ShouldNotStartNewFetchIfPreviousIsRunning()
    //{
    //    int fetchCount = 0;
    //    var query = CreateQuery(
    //        NetworkMode.Online,
    //        async (ctx) =>
    //        {
    //            fetchCount++;
    //            await Task.Delay(200);
    //            return new List<string>();
    //        },
    //        TimeSpan.Zero,
    //        true,
    //        null,
    //        TimeSpan.FromMilliseconds(100)
    //    );

    //    SetOnline(true);
    //    await query.ExecuteAsync();

    //    await Task.Delay(350); // interval triggers but previous fetch still running
    //    Assert.Equal(1, fetchCount); // still only 1 fetch
    //}

    //[Fact]
    //public async Task RefetchInterval_ShouldStopWhenDisposed()
    //{
    //    int fetchCount = 0;

    //    var query = CreateQuery(
    //        NetworkMode.Online,
    //        async (ctx) =>
    //        {
    //            fetchCount++;
    //            await Task.Delay(10);
    //            return new List<string>();
    //        },
    //        TimeSpan.Zero,
    //        true,
    //        null,
    //        TimeSpan.FromMilliseconds(100)
    //    );

    //    SetOnline(true);
    //    await query.ExecuteAsync();

    //    await Task.Delay(250);
    //    query.Dispose();

    //    int lastCount = fetchCount;
    //    await Task.Delay(200); // any refetch after dispose should not happen

    //    Assert.Equal(lastCount, fetchCount);
    //}

   
    //[Fact]
    //public async Task RefetchOnMount_WhenDataFresh_ShouldNotRefetch()
    //{
    //    SeedCache(new List<string> { "cached" }, TimeSpan.FromMinutes(5));
    //    var fetchCount = 0;

    //    var query = CreateQuery(
    //        NetworkMode.Online,
    //        _ => { fetchCount++; return FakeNetworkApi(); },
    //        staleTime: TimeSpan.FromMinutes(5),
    //        refetchOnMount: true);

    //    SetOnline(true);
    //    await ObserveQuery(query);

    //    Assert.Equal(0, fetchCount); // không fetch vì fresh
    //}

    //[Fact]
    //public async Task RefetchOnWindowFocus_WhenEnabled_ShouldRefetch()
    //{
    //    var fetchCount = 0;
    //    var query = CreateQuery(
    //        NetworkMode.Online,
    //        _ => { fetchCount++; return Task.FromResult(new List<string>()); },
    //        refetchOnWindowFocus: true);

    //    SetOnline(true);
    //    await query.ExecuteAsync();
    //    fetchCount = 0;

    //    // Simulate focus
    //    _queryClient.NotifyWindowFocus();

    //    await Task.Delay(100);
    //    Assert.Equal(1, fetchCount); // React Query refetch
    //}

    //[Fact]
    //public async Task PlaceholderData_ShouldShowBeforeFetch()
    //{
    //    var query = CreateQuery(
    //        NetworkMode.Online,
    //        _ => Task.Delay(100).ContinueWith(_ => new List<string> { "real" }),
    //        placeholderData: new List<string> { "placeholder" });

    //    var observer = new QueryObserver<List<string>>(query);
    //    await query.ExecuteAsync();

    //    var first = observer.Snapshots.First();
    //    Assert.Contains("placeholder", first.Data!);
    //}

    //[Fact]
    //public async Task Select_ShouldTransformData()
    //{
    //    var query = CreateQuery(
    //        NetworkMode.Online,
    //        _ => Task.FromResult(new List<string> { "a", "b" }),
    //        select: data => data.Select(x => x.ToUpper()).ToList());

    //    var snapshots = await ObserveQuery(query);
    //    var final = snapshots.Last();

    //    Assert.Contains("A", final.Data!);
    //    Assert.Contains("B", final.Data!);
    //}

    //[Fact]
    //public async Task GcTime_AfterInactive_ShouldRemoveFromCache()
    //{
    //    var query = CreateQuery(
    //        NetworkMode.Online,
    //        _ => Task.FromResult(new List<string> { "data" }),
    //        gcTime: TimeSpan.FromMilliseconds(100));

    //    await query.ExecuteAsync();
    //    query.Dispose();

    //    await Task.Delay(200);
    //    var entry = _queryClient.GetCacheEntry(_key);
    //    Assert.Null(entry); // bị garbage collected
    //}
}