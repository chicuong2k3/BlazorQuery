namespace BlazorQuery.Core.Tests;

public class UseQueryTests
{
    private readonly QueryClient _client = new QueryClient();

    [Fact]
    public async Task FetchData_ReturnsExpectedResult()
    {
        var query = new UseQuery<List<string>>(new QueryOptions<List<string>>(
            queryKey: new QueryKey("todos"),
            queryFn: async ctx => await FakeApi.GetTodosAsync()
        ), _client);

        await query.ExecuteAsync();

        Assert.False(query.Status == QueryStatus.Error);
        Assert.NotNull(query.Data);
        Assert.Equal(2, query.Data.Count);
    }

    [Fact]
    public async Task FetchData_ErrorIsHandled()
    {
        var query = new UseQuery<string>(new QueryOptions<string>(
            queryKey: new QueryKey("todos", -1),
            queryFn: async ctx => await FakeApi.GetTodoByIdAsync((int)ctx.QueryKey[1]!)
        ), _client);

        await query.ExecuteAsync();

        Assert.True(query.Status == QueryStatus.Error);
        Assert.NotNull(query.Error);
        Assert.Equal("Invalid ID", query.Error.Message);
    }

    [Fact]
    public async Task CacheWorks()
    {
        int callCount = 0;

        var query = new UseQuery<List<string>>(new QueryOptions<List<string>>(
            queryKey: new QueryKey("todos"),
            queryFn: async ctx => {
                callCount++;
                return await FakeApi.GetTodosAsync();
            },
            staleTime: TimeSpan.FromSeconds(10)
        ), _client);

        await query.ExecuteAsync();
        await query.ExecuteAsync();

        Assert.Equal(1, callCount); 
    }

    [Fact]
    public async Task RefetchInvalidatesCache()
    {
        int callCount = 0;

        var query = new UseQuery<List<string>>(new QueryOptions<List<string>>(
            queryKey: new QueryKey("todos"),
            queryFn: async ctx => {
                callCount++;
                return await FakeApi.GetTodosAsync();
            },
            staleTime: TimeSpan.FromSeconds(10)
        ), _client);

        await query.ExecuteAsync();
        await query.RefetchAsync();

        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task MetadataIsPassedToQueryFn()
    {
        var meta = new Dictionary<string, object> { { "filter", "active" } };
        var query = new UseQuery<List<string>>(new QueryOptions<List<string>>(
            queryKey: new QueryKey("todos"),
            queryFn: async ctx =>
            {
                if (ctx.Meta != null && ctx.Meta.TryGetValue("filter", out var filterValue))
                {
                    // Simulate filtered fetch
                    return new List<string> { $"Filtered: {(string)filterValue}" };
                }
                return await FakeApi.GetTodosAsync();
            },
            meta: meta
        ), _client);
        await query.ExecuteAsync();
        Assert.Equal(QueryStatus.Success, query.Status);
        Assert.NotNull(query.Data);
        Assert.Collection(query.Data, item => Assert.Equal("Filtered: active", item));
    }

    [Fact]
    public async Task ManualErrorThrowingWithNonThrowingClient()
    {
        var query = new UseQuery<string>(new QueryOptions<string>(
            queryKey: new QueryKey("todo", 404),
            queryFn: async ctx =>
            {
                // Simulate HttpClient response
                var fakeResponse = new { IsSuccessStatusCode = false, StatusCode = 404 };
                if (!fakeResponse.IsSuccessStatusCode)
                    throw new Exception($"Response was not ok: {fakeResponse.StatusCode}");
                return "Success";
            }
        ), _client);
        await query.ExecuteAsync();
        Assert.Equal(QueryStatus.Error, query.Status);
        Assert.Contains("not ok: 404", query.Error?.Message);
    }

    [Fact]
    public async Task SynchronousThrowIsHandled()
    {
        var query = new UseQuery<string>(new QueryOptions<string>(
            queryKey: new QueryKey("sync-error"),
            queryFn: ctx => throw new Exception("Sync error")
        ), _client);
        await query.ExecuteAsync();
        Assert.Equal(QueryStatus.Error, query.Status);
        Assert.Equal("Sync error", query.Error?.Message);
    }
}
