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

    [Fact]
    public async Task ContextDeconstruction_WorksCorrectly()
    {
        QueryKey? capturedKey = null;
        CancellationToken capturedToken = default;

        var query = new UseQuery<string>(new QueryOptions<string>(
            queryKey: new QueryKey("test", 123),
            queryFn: async ctx => {
                // Test 2-property deconstruction
                var (queryKey, signal) = ctx;
                capturedKey = queryKey;
                capturedToken = signal;
                return await Task.FromResult("success");
            }
        ), _client);

        await query.ExecuteAsync();

        Assert.Equal(QueryStatus.Success, query.Status);
        Assert.NotNull(capturedKey);
        Assert.Equal("test", capturedKey.Parts[0]);
        Assert.Equal(123, capturedKey.Parts[1]);
    }

    [Fact]
    public async Task ContextDeconstruction_WithMeta_WorksCorrectly()
    {
        var meta = new Dictionary<string, object> { { "filter", "active" } };
        IReadOnlyDictionary<string, object>? capturedMeta = null;

        var query = new UseQuery<string>(new QueryOptions<string>(
            queryKey: new QueryKey("test"),
            queryFn: async ctx => {
                // Test 3-property deconstruction
                var (queryKey, signal, m) = ctx;
                capturedMeta = m;
                return await Task.FromResult("success");
            },
            meta: meta
        ), _client);

        await query.ExecuteAsync();

        Assert.Equal(QueryStatus.Success, query.Status);
        Assert.NotNull(capturedMeta);
        Assert.True(capturedMeta.TryGetValue("filter", out var filterValue));
        Assert.Equal("active", filterValue);
    }

    [Fact]
    public async Task ReusableQueryOptions_WorksCorrectly()
    {
        // Define reusable query options factory
        static QueryOptions<string> TodoOptions(int id)
        {
            return new QueryOptions<string>(
                queryKey: new QueryKey("todo", id),
                queryFn: async ctx => {
                    var (queryKey, _) = ctx;
                    var todoId = (int)queryKey[1]!;
                    return await FakeApi.GetTodoByIdAsync(todoId);
                },
                staleTime: TimeSpan.FromSeconds(10)
            );
        }

        // Use in multiple places
        var query1 = new UseQuery<string>(TodoOptions(1), _client);
        var query2 = new UseQuery<string>(TodoOptions(2), _client);

        await query1.ExecuteAsync();
        await query2.ExecuteAsync();

        Assert.Equal(QueryStatus.Success, query1.Status);
        Assert.Equal(QueryStatus.Success, query2.Status);
        Assert.NotNull(query1.Data);
        Assert.NotNull(query2.Data);
        
        // Verify different data for different IDs
        Assert.NotEqual(query1.Data, query2.Data);
    }
}
