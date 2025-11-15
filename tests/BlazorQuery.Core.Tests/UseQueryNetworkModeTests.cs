using BlazorQuery.Core.BuildingBlocks;
using BlazorQuery.Core.Tests.Helpers;
using Moq;

namespace BlazorQuery.Core.Tests;

public class UseQueryNetworkModeTests
{
    private readonly Mock<IOnlineManager> _onlineManagerMock;
    private QueryClient _queryClient = new QueryClient();
    private readonly QueryKey _key = new("todos");

    public UseQueryNetworkModeTests()
    {
        _onlineManagerMock = new Mock<IOnlineManager>();
        _queryClient = new QueryClient(_onlineManagerMock.Object);
    }

    protected void Dispose() => _queryClient?.Dispose();

    private void SetOnline(bool isOnline) =>
        _onlineManagerMock.Setup(m => m.IsOnline).Returns(isOnline);

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
        bool refetchOnReconnect = true)
    {
        return new UseQuery<T>(
            new QueryOptions<T>(
                queryKey: _key,
                queryFn: queryFn,
                networkMode: mode,
                staleTime: staleTime ?? TimeSpan.Zero,
                refetchOnReconnect: refetchOnReconnect
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

        Assert.True(final.Status == QueryStatus.Success);
        Assert.False(final.IsLoading);
        Assert.False(final.IsPaused);
        Assert.NotNull(final.Data);
        Assert.Equal(FetchStatus.Idle, final.FetchStatus);
        Assert.Contains(snapshots, s => s.IsFetching || s.IsLoading);
    }

    [Fact]
    public async Task OnlineMode_OfflineNetwork_NoCache_ShouldPause()
    {
        SetOnline(false);
        var query = CreateQuery(NetworkMode.Online, _ => FakeNetworkApi());

        var snapshots = await ObserveQuery(query);
        var final = snapshots.Last();

        Assert.True(final.Status == QueryStatus.Pending);
        Assert.True(final.IsPaused);
        Assert.True(final.IsLoading);
        Assert.Null(final.Data);
        Assert.Equal(FetchStatus.Paused, final.FetchStatus);
    }

    [Fact]
    public async Task OnlineMode_OfflineNetwork_WithFreshCache_ShouldReturnCache()
    {
        SeedCache(new List<string> { "cached" }, TimeSpan.FromMinutes(5));
        SetOnline(false);
        var query = CreateQuery(NetworkMode.Online, _ => FakeNetworkApi(), TimeSpan.FromMinutes(5));

        var snapshots = await ObserveQuery(query);
        var final = snapshots.Last();

        Assert.True(final.Status == QueryStatus.Success);
        Assert.False(final.IsLoading);
        Assert.False(final.IsPaused);
        Assert.NotNull(final.Data);
        Assert.Equal(FetchStatus.Idle, final.FetchStatus);
    }

    [Fact]
    public async Task AlwaysMode_OfflineNetwork_ShouldError()
    {
        SetOnline(false);
        var query = CreateQuery<List<string>>(NetworkMode.Always, _ =>
        {
            throw new InvalidOperationException("Offline");
        });

        var snapshots = await ObserveQuery(query);
        var final = snapshots.Last();

        Assert.True(final.Status == QueryStatus.Error);
        Assert.False(final.IsPaused);
        Assert.NotNull(final.Error);
        Assert.Equal(FetchStatus.Idle, final.FetchStatus);
    }

    [Fact]
    public async Task OfflineFirstMode_OfflineNetwork_NoCache_ShouldPause()
    {
        SetOnline(false);
        var query = CreateQuery(NetworkMode.OfflineFirst, _ => FakeNetworkApi());

        var snapshots = await ObserveQuery(query);
        var final = snapshots.Last();

        Assert.False(final.Status == QueryStatus.Success);
        Assert.True(final.IsPaused);
        Assert.True(final.IsLoading);
        Assert.Null(final.Data);
        Assert.Equal(FetchStatus.Paused, final.FetchStatus);
    }

    [Fact]
    public async Task OfflineFirstMode_OfflineNetwork_WithFreshCache_ShouldReturnCache()
    {
        SeedCache(new List<string> { "fresh" }, TimeSpan.FromMinutes(5));
        SetOnline(false);
        var query = CreateQuery(NetworkMode.OfflineFirst, _ => FakeNetworkApi(), TimeSpan.FromMinutes(5));

        var snapshots = await ObserveQuery(query);
        var final = snapshots.Last();

        Assert.True(final.Status == QueryStatus.Success);
        Assert.False(final.IsLoading);
        Assert.False(final.IsPaused);
        Assert.Contains("fresh", final.Data!);
        Assert.Equal(FetchStatus.Idle, final.FetchStatus);
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

        Assert.True(final.Status == QueryStatus.Success);
        Assert.True(final.IsPaused);
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

        var observerTask = ObserveQuery(query);

        await Task.Delay(50);
        SetOnline(true); // simulate reconnect
        await Task.Delay(100);

        var snapshots = await observerTask;
        var final = snapshots.Last();

        Assert.True(refetchCount > 0);
        Assert.True(final.Status == QueryStatus.Success);
        Assert.Contains("network", final.Data![0]);
    }

    [Fact]
    public async Task BackgroundRefetch_ShouldSetIsFetchingBackground()
    {
        SeedCache(new List<string> { "stale" }, TimeSpan.FromMilliseconds(500));
        await Task.Delay(50);
        SetOnline(true);

        var query = CreateQuery(NetworkMode.OfflineFirst, _ => FakeNetworkApi(), TimeSpan.FromMilliseconds(1));
        var snapshots = await ObserveQuery(query);
        var final = snapshots.Last();

        // Background fetch có trigger
        Assert.True(snapshots.Any(s => s.IsFetchingBackground));
        Assert.False(snapshots.First().IsInitialLoading); // data existed
        Assert.True(final.Status == QueryStatus.Success);
        Assert.False(final.IsFetchingBackground);
    }

    [Fact]
    public async Task OnlineMode_OfflineMidFetch_ThenReconnect_ShouldRefetchFromStart()
    {
        var tcsFirstFetch = new TaskCompletionSource<List<string>>();
        var tcsSecondFetch = new TaskCompletionSource<List<string>>();
        var fetchCount = 0;

        var queryFn = new Func<QueryFunctionContext, Task<List<string>>>(async ctx =>
        {
            fetchCount++;
            if (fetchCount == 1)
                return await tcsFirstFetch.Task; // first fetch will be cancelled
            else
                return await tcsSecondFetch.Task; // second fetch after reconnect
        });

        SetOnline(true);
        var query = CreateQuery(NetworkMode.Online, queryFn);
        var observerTask = ObserveQuery(query);

        await Task.Delay(20); // mid-fetch
        SetOnline(false);     // simulate offline

        // mid-fetch should be cancelled
        tcsFirstFetch.TrySetResult(new List<string> { "should not be used" });
        await Task.Delay(50);

        Assert.True(query.IsPaused);

        // restore network
        SetOnline(true);
        tcsSecondFetch.TrySetResult(new List<string> { "refetched" });

        var snapshots = await observerTask;
        var final = snapshots.Last();

        Assert.True(final.Status == QueryStatus.Success);
        Assert.Contains("refetched", final.Data!);
        Assert.Equal(FetchStatus.Idle, final.FetchStatus);
        Assert.Equal(2, fetchCount); // first cancelled, second refetch
    }
}
