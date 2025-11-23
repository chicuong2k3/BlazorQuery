using BlazorQuery.Core.Tests.Helpers;
using Moq;

namespace BlazorQuery.Core.Tests;

public class UseQueryTestsBase
{
    protected readonly Mock<IOnlineManager> _onlineManagerMock;
    private QueryClient _queryClient;
    private readonly QueryKey _key = new("todos");

    public UseQueryTestsBase()
    {
        _onlineManagerMock = new Mock<IOnlineManager>();
        _queryClient = new QueryClient(_onlineManagerMock.Object);
    }

    protected void Dispose() => _queryClient?.Dispose();
    protected void SetOnline(bool isOnline)
    {
        _onlineManagerMock.Setup(m => m.IsOnline).Returns(isOnline);
    }

    protected static async Task<List<string>> FakeNetworkApi()
    {
        await Task.Delay(50);
        return new List<string> { "network-1", "network-2" };
    }

    protected void SeedCache<T>(T data, TimeSpan staleTime)
    {
        _queryClient.Set(_key, data);
        var entry = _queryClient.GetCacheEntry(_key);
        if (entry != null)
            entry.FetchTime = DateTime.UtcNow - TimeSpan.FromMilliseconds(100);
    }

    protected UseQuery<T> CreateQuery<T>(
        NetworkMode mode,
        Func<QueryFunctionContext, Task<T>> queryFn,
        TimeSpan? staleTime = null,
        bool refetchOnReconnect = true,
        int? retry = null,
        bool retryInfinite = false,
        Func<int, Exception, bool>? retryFunc = null,
        Func<int, TimeSpan>? retryDelayFunc = null,
        TimeSpan? refetchInterval = null)
    {
        return new UseQuery<T>(
            new QueryOptions<T>(
                queryKey: _key,
                queryFn: queryFn,
                networkMode: mode,
                staleTime: staleTime ?? TimeSpan.Zero,
                refetchOnReconnect: refetchOnReconnect,
                retry: retry,
                retryInfinite: retryInfinite,
                retryFunc: retryFunc,
                retryDelayFunc: retryDelayFunc,
                refetchInterval: refetchInterval
            ),
            _queryClient);
    }

    protected async Task<List<QuerySnapshot<T>>> ObserveQuery<T>(UseQuery<T> query)
    {
        using var observer = new QueryObserver<T>(query);
        await observer.ExecuteAsync();
        return observer.Snapshots;
    }
}
