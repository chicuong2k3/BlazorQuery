namespace BlazorQuery.Core.Tests;

public class InitialQueryDataTests : IDisposable
{
    private readonly QueryClient _client = new();

    [Fact]
    public Task InitialData_ShouldPrepopulateCache()
    {
        var initialTodos = new List<string> { "Todo 1", "Todo 2" };
        
        var query = new UseQuery<List<string>>(
            new QueryOptions<List<string>>(
                queryKey: new QueryKey("todos"),
                queryFn: async _ => {
                    await Task.Delay(10);
                    return ["Fetched Todo"];
                },
                initialData: initialTodos
            ),
            _client
        );

        // Should have initial data immediately
        Assert.Equal(QueryStatus.Success, query.Status);
        Assert.Equal(initialTodos, query.Data);
        Assert.Equal(2, query.Data!.Count);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task InitialData_WithoutStaleTime_ShouldRefetchImmediately()
    {
        var fetchCount = 0;
        var initialData = "Initial";
        
        var query = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("data"),
                queryFn: async _ => {
                    fetchCount++;
                    await Task.Delay(10);
                    return "Fetched";
                },
                initialData: initialData
                // No staleTime = 0 (immediately stale)
            ),
            _client
        );

        // Has initial data
        Assert.Equal("Initial", query.Data);
        Assert.Equal(0, fetchCount);

        // Execute should refetch (data is immediately stale)
        await query.ExecuteAsync();

        Assert.Equal(1, fetchCount);
        Assert.Equal("Fetched", query.Data);
    }

    [Fact]
    public async Task InitialData_WithStaleTime_ShouldNotRefetchIfFresh()
    {
        var fetchCount = 0;
        var initialData = "Initial";
        
        var query = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("data"),
                queryFn: async _ => {
                    fetchCount++;
                    await Task.Delay(10);
                    return "Fetched";
                },
                initialData: initialData,
                staleTime: TimeSpan.FromSeconds(10) // Fresh for 10 seconds
            ),
            _client
        );

        // Has initial data
        Assert.Equal("Initial", query.Data);

        // Execute should NOT refetch (data is still fresh)
        await query.ExecuteAsync();

        Assert.Equal(0, fetchCount);
        Assert.Equal("Initial", query.Data); // Still initial data
    }

    [Fact]
    public async Task InitialData_WithStaleTime_ShouldRefetchAfterExpiry()
    {
        var fetchCount = 0;
        var initialData = "Initial";
        
        var query = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("data"),
                queryFn: async _ => {
                    fetchCount++;
                    await Task.Delay(10);
                    return "Fetched";
                },
                initialData: initialData,
                staleTime: TimeSpan.FromMilliseconds(50) // Short stale time
            ),
            _client
        );

        Assert.Equal("Initial", query.Data);

        // Wait for data to become stale
        await Task.Delay(100);

        // Now should refetch
        await query.ExecuteAsync();

        Assert.Equal(1, fetchCount);
        Assert.Equal("Fetched", query.Data);
    }

    [Fact]
    public async Task InitialDataUpdatedAt_ShouldRespectProvidedTimestamp()
    {
        var fetchCount = 0;
        var tenSecondsAgo = DateTime.UtcNow.AddSeconds(-10);
        
        var query = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("data"),
                queryFn: async _ => {
                    fetchCount++;
                    await Task.Delay(10);
                    return "Fetched";
                },
                initialData: "Initial",
                staleTime: TimeSpan.FromSeconds(5), // 5 second freshness
                initialDataUpdatedAt: tenSecondsAgo // Data is 10 seconds old
            ),
            _client
        );

        // Has initial data immediately
        Assert.Equal("Initial", query.Data);
        Assert.Equal(QueryStatus.Success, query.Status);
        Assert.Equal(0, fetchCount);

        // Data is 10 seconds old, staleTime is 5 seconds
        // So data should be considered stale and will refetch
        await query.ExecuteAsync();

        Assert.Equal(1, fetchCount);
        Assert.Equal("Fetched", query.Data);
    }

    [Fact]
    public async Task InitialDataUpdatedAt_WithFreshTimestamp_ShouldNotRefetch()
    {
        var fetchCount = 0;
        var twoSecondsAgo = DateTime.UtcNow.AddSeconds(-2);
        
        var query = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("data"),
                queryFn: async _ => {
                    fetchCount++;
                    await Task.Delay(10);
                    return "Fetched";
                },
                initialData: "Initial",
                staleTime: TimeSpan.FromSeconds(10), // 10 second freshness
                initialDataUpdatedAt: twoSecondsAgo // Data is 2 seconds old
            ),
            _client
        );

        // Has initial data
        Assert.Equal("Initial", query.Data);
        Assert.Equal(QueryStatus.Success, query.Status);
        Assert.Equal(0, fetchCount);

        // Data is 2 seconds old, staleTime is 10 seconds
        // So data should still be fresh and won't refetch
        await query.ExecuteAsync();

        Assert.Equal(0, fetchCount);
        Assert.Equal("Initial", query.Data); // Still initial data
    }

    [Fact]
    public async Task InitialData_StalenessLogic_VerifyExecuteAsyncBehavior()
    {
        var fetchCount = 0;
        
        // Test 1: Stale initial data (should refetch)
        var oldTimestamp = DateTime.UtcNow.AddSeconds(-100);
        var query1 = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("test1"),
                queryFn: async _ => {
                    fetchCount++;
                    return "Fetched";
                },
                initialData: "Initial",
                staleTime: TimeSpan.FromSeconds(5),
                initialDataUpdatedAt: oldTimestamp
            ),
            _client
        );

        Assert.Equal("Initial", query1.Data);
        await query1.ExecuteAsync();
        Assert.Equal(1, fetchCount); // Should refetch (stale)
        Assert.Equal("Fetched", query1.Data);

        // Test 2: Fresh initial data (should NOT refetch)
        fetchCount = 0;
        var recentTimestamp = DateTime.UtcNow.AddSeconds(-2);
        var query2 = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("test2"),
                queryFn: async _ => {
                    fetchCount++;
                    return "Fetched";
                },
                initialData: "Initial",
                staleTime: TimeSpan.FromSeconds(10),
                initialDataUpdatedAt: recentTimestamp
            ),
            _client
        );

        Assert.Equal("Initial", query2.Data);
        await query2.ExecuteAsync();
        Assert.Equal(0, fetchCount); // Should NOT refetch (still fresh)
        Assert.Equal("Initial", query2.Data);
    }

    [Fact]
    public void InitialDataFunc_ShouldBeLazyEvaluated()
    {
        var funcCallCount = 0;
        
        var query = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("data"),
                queryFn: _ => Task.FromResult("Fetched"),
                initialDataFunc: () => {
                    funcCallCount++;
                    return "Lazy Initial";
                }
            ),
            _client
        );

        // Function should be called once during initialization
        Assert.Equal(1, funcCallCount);
        Assert.Equal("Lazy Initial", query.Data);

        // Creating another query should call function again

        Assert.Equal(2, funcCallCount);
    }

    [Fact]
    public void InitialDataFromCache_ShouldWork()
    {
        // First, populate cache with todos list
        var todosList = new List<Todo>
        {
            new() { Id = 1, Title = "Todo 1" },
            new() { Id = 2, Title = "Todo 2" },
            new() { Id = 3, Title = "Todo 3" }
        };
        
        _client.SetQueryData(new QueryKey("todos"), todosList);

        var todoId = 2;
        var query = new UseQuery<Todo>(
            new QueryOptions<Todo>(
                queryKey: new QueryKey("todo", todoId),
                queryFn: _ => Task.FromResult(new Todo { Id = todoId, Title = "Fetched Todo" }),
                initialDataFunc: () => {
                    // Get from cache
                    var todos = _client.GetQueryData<List<Todo>>(new QueryKey("todos"));
                    return todos?.Find(t => t.Id == todoId);
                }
            ),
            _client
        );

        // Should have initial data from cache
        Assert.NotNull(query.Data);
        Assert.Equal(2, query.Data!.Id);
        Assert.Equal("Todo 2", query.Data.Title);
    }

    [Fact]
    public void InitialDataFromCache_WithDataUpdatedAt_ShouldWork()
    {
        // Populate cache
        var todosList = new List<Todo>
        {
            new() { Id = 1, Title = "Todo 1" },
            new() { Id = 2, Title = "Todo 2" }
        };
        
        _client.SetQueryData(new QueryKey("todos"), todosList);

        var todoId = 2;
        var query = new UseQuery<Todo>(
            new QueryOptions<Todo>(
                queryKey: new QueryKey("todo", todoId),
                queryFn: _ => Task.FromResult(new Todo { Id = todoId, Title = "Fetched Todo" }),
                initialDataFunc: () => {
                    var todos = _client.GetQueryData<List<Todo>>(new QueryKey("todos"));
                    return todos?.Find(t => t.Id == todoId);
                },
                initialDataUpdatedAt: _client.GetQueryState(new QueryKey("todos"))?.DataUpdatedAt,
                staleTime: TimeSpan.FromSeconds(10)
            ),
            _client
        );

        // Should have data with proper timestamp
        Assert.NotNull(query.Data);
        Assert.Equal("Todo 2", query.Data!.Title);
    }

    [Fact]
    public void ConditionalInitialDataFromCache_ShouldOnlyUseIfFresh()
    {
        // Populate cache with old data
        var oldData = new List<string> { "Old Todo" };
        _client.SetQueryData(new QueryKey("todos"), oldData);
        
        // Make it old by setting fetch time in the past
        var entry = _client.GetCacheEntry(new QueryKey("todos"));
        if (entry != null)
        {
            entry.FetchTime = DateTime.UtcNow.AddSeconds(-20); // 20 seconds ago
        }

        var query = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("todo", 1),
                queryFn: _ => Task.FromResult("Fetched Todo"),
                initialDataFunc: () => {
                    var state = _client.GetQueryState(new QueryKey("todos"));
                    
                    // Only use if data is no older than 10 seconds
                    if (state != null && (DateTime.UtcNow - state.DataUpdatedAt).TotalSeconds <= 10)
                    {
                        var todos = state.Data as List<string>;
                        return todos?.FirstOrDefault();
                    }
                    
                    // Return null to let it fetch
                    return null;
                },
                staleTime: TimeSpan.FromSeconds(5)
            ),
            _client
        );

        // Should NOT have initial data (too old)
        Assert.Null(query.Data);
        Assert.Equal(QueryStatus.Pending, query.Status);
    }

    [Fact]
    public void ConditionalInitialDataFromCache_ShouldUseIfFresh()
    {
        // Populate cache with fresh data
        var freshData = new List<string> { "Fresh Todo" };
        _client.SetQueryData(new QueryKey("todos"), freshData);

        var query = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("todo", 1),
                queryFn: _ => Task.FromResult("Fetched Todo"),
                initialDataFunc: () => {
                    var state = _client.GetQueryState(new QueryKey("todos"));
                    
                    // Only use if data is no older than 10 seconds
                    if (state != null && (DateTime.UtcNow - state.DataUpdatedAt).TotalSeconds <= 10)
                    {
                        var todos = state.Data as List<string>;
                        return todos?.FirstOrDefault();
                    }
                    
                    return null;
                },
                staleTime: TimeSpan.FromSeconds(5)
            ),
            _client
        );

        // Should have initial data (fresh enough)
        Assert.NotNull(query.Data);
        Assert.Equal("Fresh Todo", query.Data);
        Assert.Equal(QueryStatus.Success, query.Status);
    }

    public void Dispose() => _client.Dispose();

    // Helper class
    private class Todo
    {
        public int Id { get; init; }
        public string Title { get; init; } = string.Empty;
    }
}

