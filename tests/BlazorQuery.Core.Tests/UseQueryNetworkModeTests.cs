using BlazorQuery.Core.BuildingBlocks;
namespace BlazorQuery.Core.Tests;

public class UseQueryNetworkModeTests
{
    private QueryClient _client = new QueryClient();

    // Helper to reset state
    private void Reset()
    {
        _client = new QueryClient();
    }

    
    [Fact]
    public async Task OnlineMode_Online_FetchingToIdle_Success()
    {
        Reset();
        OnlineManager.IsOnline = true;

        var query = new UseQuery<List<string>>(new QueryOptions<List<string>>(
            queryKey: new QueryKey("todos"),
            queryFn: async ctx => await FakeApi.GetTodosAsync(),
            networkMode: NetworkMode.Online
        ), _client);

        Assert.True(query.IsLoading);
        Assert.True(query.IsFetching);
        Assert.False(query.IsPaused);
        Assert.Equal(FetchStatus.Fetching, query.FetchStatus);

        await query.ExecuteAsync();

        Assert.False(query.IsLoading);
        Assert.False(query.IsFetching);
        Assert.False(query.IsPaused);
        Assert.True(query.IsSuccess);
        Assert.False(query.IsError);
        Assert.Equal(FetchStatus.Idle, query.FetchStatus);
    }

    [Fact]
    public async Task OnlineMode_Offline_NoData_Paused_IsLoading()
    {
        Reset();
        OnlineManager.IsOnline = false;

        var query = new UseQuery<List<string>>(new QueryOptions<List<string>>(
            queryKey: new QueryKey("todos"),
            queryFn: async ctx => await FakeApi.GetTodosAsync(),
            networkMode: NetworkMode.Online
        ), _client);

        await query.ExecuteAsync();

        Assert.True(query.IsLoading);  // Data == null
        Assert.False(query.IsFetching);
        Assert.True(query.IsPaused);
        Assert.False(query.IsSuccess);
        Assert.False(query.IsError);
        Assert.Equal(FetchStatus.Paused, query.FetchStatus);
    }

    [Fact]
    public async Task OnlineMode_Offline_WithFreshData_Paused_ButNotLoading()
    {
        Reset();
        var key = new QueryKey("todos");
        _client.Set(key, new List<string> { "cached" }, staleTime: TimeSpan.FromMinutes(5));

        OnlineManager.IsOnline = false;

        var query = new UseQuery<List<string>>(new QueryOptions<List<string>>(
            queryKey: key,
            queryFn: async ctx => await FakeApi.GetTodosAsync(),
            staleTime: TimeSpan.FromMinutes(10),
            networkMode: NetworkMode.Online
        ), _client);

        await query.ExecuteAsync();

        Assert.False(query.IsLoading); // Data exists
        Assert.False(query.IsFetching);
        Assert.True(query.IsPaused);   // Still paused because online-only
        Assert.True(query.IsSuccess);
        Assert.False(query.IsError);
        Assert.Equal(FetchStatus.Paused, query.FetchStatus);
    }

    // ================================
    // ALWAYS MODE
    // ================================

    [Fact]
    public async Task AlwaysMode_Online_FetchingToIdle_Success()
    {
        Reset();
        OnlineManager.IsOnline = true;

        var query = new UseQuery<string>(new QueryOptions<string>(
            queryKey: new QueryKey("todo", 1),
            queryFn: async ctx => await FakeApi.GetTodoByIdAsync((int)ctx.QueryKey[1]!),
            networkMode: NetworkMode.Always
        ), _client);

        Assert.True(query.IsLoading);
        Assert.True(query.IsFetching);
        Assert.False(query.IsPaused);
        Assert.Equal(FetchStatus.Fetching, query.FetchStatus);

        await query.ExecuteAsync();

        Assert.False(query.IsLoading);
        Assert.False(query.IsFetching);
        Assert.False(query.IsPaused);
        Assert.True(query.IsSuccess);
        Assert.False(query.IsError);
        Assert.Equal(FetchStatus.Idle, query.FetchStatus);
    }

    [Fact]
    public async Task AlwaysMode_Offline_FetchingToIdle_Error_IsLoading()
    {
        Reset();
        OnlineManager.IsOnline = false;

        var query = new UseQuery<string>(new QueryOptions<string>(
            queryKey: new QueryKey("todo", -1),
            queryFn: async ctx => await FakeApi.GetTodoByIdAsync((int)ctx.QueryKey[1]!),
            networkMode: NetworkMode.Always
        ), _client);

        Assert.True(query.IsLoading);
        Assert.True(query.IsFetching);
        Assert.False(query.IsPaused);
        Assert.Equal(FetchStatus.Fetching, query.FetchStatus);

        await Assert.ThrowsAsync<Exception>(() => query.ExecuteAsync());

        Assert.True(query.IsLoading);  // Data still null
        Assert.False(query.IsFetching);
        Assert.False(query.IsPaused);
        Assert.True(query.IsError);
        Assert.False(query.IsSuccess);
        Assert.Equal(FetchStatus.Idle, query.FetchStatus);
    }

    // ================================
    // OFFLINE-FIRST MODE
    // ================================

    [Fact]
    public async Task OfflineFirstMode_Online_FetchingToIdle_Success()
    {
        Reset();
        OnlineManager.IsOnline = true;

        var query = new UseQuery<List<string>>(new QueryOptions<List<string>>(
            queryKey: new QueryKey("todos"),
            queryFn: async ctx => await FakeApi.GetTodosAsync(),
            networkMode: NetworkMode.OfflineFirst
        ), _client);

        Assert.True(query.IsLoading);
        Assert.True(query.IsFetching);
        Assert.False(query.IsPaused);
        Assert.Equal(FetchStatus.Fetching, query.FetchStatus);

        await query.ExecuteAsync();

        Assert.False(query.IsLoading);
        Assert.False(query.IsFetching);
        Assert.False(query.IsPaused);
        Assert.True(query.IsSuccess);
        Assert.False(query.IsError);
        Assert.Equal(FetchStatus.Idle, query.FetchStatus);
    }

    [Fact]
    public async Task OfflineFirstMode_Offline_DataStale_Paused_IsLoading()
    {
        Reset();
        OnlineManager.IsOnline = false;

        var query = new UseQuery<List<string>>(new QueryOptions<List<string>>(
            queryKey: new QueryKey("todos"),
            queryFn: async ctx => await FakeApi.GetTodosAsync(),
            staleTime: TimeSpan.Zero, // force stale
            networkMode: NetworkMode.OfflineFirst
        ), _client);

        await query.ExecuteAsync();

        Assert.True(query.IsLoading);
        Assert.False(query.IsFetching);
        Assert.True(query.IsPaused);
        Assert.False(query.IsSuccess);
        Assert.False(query.IsError);
        Assert.Equal(FetchStatus.Paused, query.FetchStatus);
    }

    [Fact]
    public async Task OfflineFirstMode_Offline_DataFresh_Idle_IsSuccess_NoLoading()
    {
        Reset();

        // Pre-populate cache with fresh data
        var key = new QueryKey("todos");
        _client.Set(key, new List<string> { "a", "b" }, staleTime: TimeSpan.FromMinutes(10));

        OnlineManager.IsOnline = false;

        var query = new UseQuery<List<string>>(new QueryOptions<List<string>>(
            queryKey: key,
            queryFn: async ctx => await FakeApi.GetTodosAsync(),
            staleTime: TimeSpan.FromMinutes(10),
            networkMode: NetworkMode.OfflineFirst
        ), _client);

        await query.ExecuteAsync();

        Assert.False(query.IsLoading);
        Assert.False(query.IsFetching);
        Assert.False(query.IsPaused);
        Assert.True(query.IsSuccess);
        Assert.False(query.IsError);
        Assert.Equal(FetchStatus.Idle, query.FetchStatus);
    }

    [Fact]
    public async Task OfflineFirstMode_Offline_NoData_Paused_IsLoading()
    {
        Reset();
        OnlineManager.IsOnline = false;

        var query = new UseQuery<List<string>>(new QueryOptions<List<string>>(
            queryKey: new QueryKey("todos"),
            queryFn: async ctx => await FakeApi.GetTodosAsync(),
            networkMode: NetworkMode.OfflineFirst
        ), _client);

        await query.ExecuteAsync();

        Assert.True(query.IsLoading);
        Assert.False(query.IsFetching);
        Assert.True(query.IsPaused);
        Assert.False(query.IsSuccess);
        Assert.False(query.IsError);
        Assert.Equal(FetchStatus.Paused, query.FetchStatus);
    }
}