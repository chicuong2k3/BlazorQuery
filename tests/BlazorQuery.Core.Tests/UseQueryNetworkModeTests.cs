using BlazorQuery.Core.Tests.Helpers;
using Moq;

namespace BlazorQuery.Core.Tests;

public class UseQueryNetworkModeTests
{
    private readonly Mock<IOnlineManager> _onlineManagerMock;
    private QueryClient _queryClient;
    private readonly QueryKey _key = new("todos");

    public UseQueryNetworkModeTests()
    {
        _onlineManagerMock = new Mock<IOnlineManager>();
        _queryClient = new QueryClient(_onlineManagerMock.Object);
    }

    protected void Dispose() => _queryClient?.Dispose();

    private void SetOnline(bool isOnline)
    {
        _onlineManagerMock.Setup(m => m.IsOnline).Returns(isOnline);
    }

    private static async Task<List<string>> FakeNetworkApi()
    {
        await Task.Delay(50);
        return new List<string> { "network-1", "network-2" };
    }

    private void SeedCache<T>(T data, TimeSpan staleTime)
    {
        _queryClient.Set(_key, data);
        var entry = _queryClient.GetCacheEntry(_key);
        if (entry != null)
            entry.FetchTime = DateTime.UtcNow - TimeSpan.FromMilliseconds(100);
    }

    private UseQuery<T> CreateQuery<T>(
        NetworkMode mode,
        Func<QueryFunctionContext, Task<T>> queryFn,
        TimeSpan? staleTime = null,
        bool refetchOnReconnect = true,
        int? retry = null)
    {
        return new UseQuery<T>(
            new QueryOptions<T>(
                queryKey: _key,
                queryFn: queryFn,
                networkMode: mode,
                staleTime: staleTime ?? TimeSpan.Zero,
                refetchOnReconnect: refetchOnReconnect,
                retry: retry
            ),
            _queryClient);
    }

    private async Task<List<QuerySnapshot<T>>> ObserveQuery<T>(UseQuery<T> query)
    {
        using var observer = new QueryObserver<T>(query);
        await observer.ExecuteAsync();
        return observer.Snapshots;
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
        _onlineManagerMock.Raise(m => m.OnlineStatusChanged += null);
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
        SetOnline(false);
        query.HandleOffline();

        // Swallow cancellation
        try { await fetchTask; } catch (OperationCanceledException) { }

        Assert.Equal(FetchStatus.Paused, query.FetchStatus);

        // Reconnect
        SetOnline(true);
        _onlineManagerMock.Raise(m => m.OnlineStatusChanged += null);

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
    //public async Task RefetchInterval_ShouldPollAtInterval()
    //{
    //    var fetchCount = 0;
    //    var query = CreateQuery(
    //        NetworkMode.Online,
    //        _ => { fetchCount++; return Task.FromResult(new List<string>()); },
    //        refetchInterval: TimeSpan.FromMilliseconds(100));

    //    SetOnline(true);
    //    await query.ExecuteAsync();

    //    await Task.Delay(350);
    //    Assert.True(fetchCount >= 3); 
    //}

    [Fact]
    public async Task Retry_WhenFetchFails_ShouldRetrySpecifiedTimes()
    {
        var fetchCount = 0;
        var tcs = new TaskCompletionSource<List<string>>();

        var query = CreateQuery<List<string>>(
            NetworkMode.Online,
            _ =>
            {
                fetchCount++;
                if (fetchCount < 4)
                    throw new Exception("Fail");
                return Task.FromResult(new List<string> { "success" });
            },
            retry: 3);

        SetOnline(true);
        var snapshots = await ObserveQuery(query);
        var final = snapshots.Last();

        Assert.Equal(QueryStatus.Success, final.Status);
        Assert.Equal(4, fetchCount); 
        Assert.Equal(3, query.FailureCount);
    }

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