namespace BlazorQuery.Core.Tests;

public class PlaceholderQueryDataTests : IDisposable
{
    private readonly QueryClient _client = new();

    [Fact]
    public void PlaceholderData_ShouldNotPersistToCache()
    {
        var placeholderTodos = new List<string> { "Placeholder 1", "Placeholder 2" };
        
        var query = new UseQuery<List<string>>(
            new QueryOptions<List<string>>(
                queryKey: new QueryKey("todos"),
                queryFn: async _ => {
                    await Task.Delay(10);
                    return ["Real Todo"];
                },
                placeholderData: placeholderTodos
            ),
            _client
        );

        // Should have placeholder data immediately
        Assert.Equal(placeholderTodos, query.Data);
        Assert.True(query.IsPlaceholderData);
        Assert.Equal(QueryStatus.Success, query.Status);

        // But cache should be empty (placeholder not persisted)
        var cached = _client.GetQueryData<List<string>>(new QueryKey("todos"));
        Assert.Null(cached);
    }

    [Fact]
    public async Task PlaceholderData_ShouldBeReplacedByRealData()
    {
        var placeholderData = "Placeholder";
        
        var query = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("data"),
                queryFn: async _ => {
                    await Task.Delay(10);
                    return "Real Data";
                },
                placeholderData: placeholderData
            ),
            _client
        );

        // Has placeholder initially
        Assert.Equal("Placeholder", query.Data);
        Assert.True(query.IsPlaceholderData);

        // Fetch real data
        await query.ExecuteAsync();

        // Should have real data now
        Assert.Equal("Real Data", query.Data);
        Assert.False(query.IsPlaceholderData);
        
        // Real data should be in cache
        var cached = _client.GetQueryData<string>(new QueryKey("data"));
        Assert.Equal("Real Data", cached);
    }

    [Fact]
    public void PlaceholderData_ShouldStartInSuccessState()
    {
        var query = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("data"),
                queryFn: _ => Task.FromResult("Real Data"),
                placeholderData: "Placeholder"
            ),
            _client
        );

        // Should be in Success state (not Pending)
        Assert.Equal(QueryStatus.Success, query.Status);
        Assert.True(query.IsSuccess);
        Assert.False(query.IsPending);
        Assert.True(query.IsPlaceholderData);
    }

    [Fact]
    public void PlaceholderDataFunc_ShouldBeCalled()
    {
        var funcCallCount = 0;
        
        var query = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("data"),
                queryFn: _ => Task.FromResult("Real Data"),
                placeholderDataFunc: (_, _) => {
                    funcCallCount++;
                    return "Computed Placeholder";
                }
            ),
            _client
        );

        Assert.Equal(1, funcCallCount);
        Assert.Equal("Computed Placeholder", query.Data);
        Assert.True(query.IsPlaceholderData);
    }

    [Fact]
    public async Task PlaceholderDataFunc_ShouldReceivePreviousData()
    {
        // First query with real data
        var query1 = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("data", 1),
                queryFn: async _ => {
                    await Task.Delay(10);
                    return "Data 1";
                }
            ),
            _client
        );

        await query1.ExecuteAsync();
        Assert.Equal("Data 1", query1.Data);

        // Create second query - should have access to previous data
        string? receivedPrevData = null;
        QueryOptions<string>? receivedPrevQuery = null;
        
        var query2 = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("data", 2),
                queryFn: async _ => {
                    await Task.Delay(10);
                    return "Data 2";
                },
                placeholderDataFunc: (prevData, prevQuery) => {
                    receivedPrevData = prevData;
                    receivedPrevQuery = prevQuery;
                    return prevData; // Use previous data as placeholder
                }
            ),
            _client
        );

        // Should have previous data as placeholder
        Assert.Equal("Data 1", query2.Data);
        Assert.True(query2.IsPlaceholderData);
        Assert.Equal("Data 1", receivedPrevData);
        Assert.NotNull(receivedPrevQuery);

        // Fetch real data
        await query2.ExecuteAsync();
        
        // Now has real data
        Assert.Equal("Data 2", query2.Data);
        Assert.False(query2.IsPlaceholderData);
    }

    [Fact]
    public void PlaceholderDataFromCache_ShouldWork()
    {
        // First, populate cache with blog posts list
        var blogPosts = new List<BlogPost>
        {
            new() { Id = 1, Title = "Post 1", Body = "Full content 1" },
            new() { Id = 2, Title = "Post 2", Body = "Full content 2" },
            new() { Id = 3, Title = "Post 3", Body = "Full content 3" }
        };
        
        _client.SetQueryData(new QueryKey("blogPosts"), blogPosts);

        // Now create individual post query using preview from cache
        var postId = 2;
        var query = new UseQuery<BlogPost>(
            new QueryOptions<BlogPost>(
                queryKey: new QueryKey("blogPost", postId),
                queryFn: async _ => {
                    await Task.Delay(10);
                    return new BlogPost 
                    { 
                        Id = postId, 
                        Title = "Post 2", 
                        Body = "Full detailed content from server"
                    };
                },
                placeholderDataFunc: (_, _) => {
                    // Use preview from list as placeholder
                    var posts = _client.GetQueryData<List<BlogPost>>(new QueryKey("blogPosts"));
                    return posts?.Find(p => p.Id == postId);
                }
            ),
            _client
        );

        // Should have placeholder from cache
        Assert.NotNull(query.Data);
        Assert.Equal("Post 2", query.Data!.Title);
        Assert.Equal("Full content 2", query.Data.Body); // Preview version
        Assert.True(query.IsPlaceholderData);
    }

    [Fact]
    public async Task PlaceholderData_WithFetch_ShouldShowBothStates()
    {
        var query = new UseQuery<BlogPost>(
            new QueryOptions<BlogPost>(
                queryKey: new QueryKey("post"),
                queryFn: async _ => {
                    await Task.Delay(50);
                    return new BlogPost 
                    { 
                        Id = 1, 
                        Title = "Real Post", 
                        Body = "Real full content"
                    };
                },
                placeholderData: new BlogPost 
                { 
                    Id = 1, 
                    Title = "Placeholder Post", 
                    Body = "Preview..."
                }
            ),
            _client
        );

        // State 1: Has placeholder, Status = Success, IsPlaceholderData = true
        Assert.Equal("Placeholder Post", query.Data!.Title);
        Assert.Equal(QueryStatus.Success, query.Status);
        Assert.True(query.IsPlaceholderData);
        Assert.False(query.IsFetching);

        // Start fetching
        var fetchTask = query.ExecuteAsync();
        await Task.Delay(10); // Let it start

        // State 2: Still has placeholder, but fetching in background
        Assert.Equal("Placeholder Post", query.Data!.Title);
        Assert.True(query.IsPlaceholderData);
        Assert.True(query.IsFetching);

        // Wait for completion
        await fetchTask;

        // State 3: Has real data, IsPlaceholderData = false
        Assert.Equal("Real Post", query.Data!.Title);
        Assert.Equal("Real full content", query.Data.Body);
        Assert.False(query.IsPlaceholderData);
        Assert.False(query.IsFetching);
    }

    [Fact]
    public void InitialData_TakesPriorityOverPlaceholderData()
    {
        var query = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("data"),
                queryFn: _ => Task.FromResult("Fetched Data"),
                initialData: "Initial Data",      // Priority 1
                placeholderData: "Placeholder"    // Priority 2
            ),
            _client
        );

        // Should use initial data (not placeholder)
        Assert.Equal("Initial Data", query.Data);
        Assert.False(query.IsPlaceholderData);
        
        // Initial data should be in cache
        var cached = _client.GetQueryData<string>(new QueryKey("data"));
        Assert.Equal("Initial Data", cached);
    }

    [Fact]
    public void PlaceholderData_WithNullValue_ShouldNotBeUsed()
    {
        var query = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("data"),
                queryFn: _ => Task.FromResult("Fetched Data"),
                placeholderDataFunc: (_, _) => null // Returns null
            ),
            _client
        );

        // Should not have data (null placeholder ignored)
        Assert.Null(query.Data);
        Assert.False(query.IsPlaceholderData);
        Assert.Equal(QueryStatus.Pending, query.Status);
    }

    [Fact]
    public async Task PlaceholderData_ShouldWorkWithConditionalLogic()
    {
        // Populate cache with old data
        _client.SetQueryData(new QueryKey("posts"), new List<Post> 
        { 
            new() { Id = 1, Title = "Cached Post" } 
        });

        var query = new UseQuery<Post>(
            new QueryOptions<Post>(
                queryKey: new QueryKey("post", 1),
                queryFn: async _ => {
                    await Task.Delay(10);
                    return new Post { Id = 1, Title = "Fresh Post" };
                },
                placeholderDataFunc: (_, _) => {
                    var state = _client.GetQueryState(new QueryKey("posts"));
                    
                    // Only use as placeholder if relatively fresh
                    if (state != null && 
                        (DateTime.UtcNow - state.DataUpdatedAt).TotalSeconds < 60)
                    {
                        var posts = state.Data as List<Post>;
                        return posts?.Find(p => p.Id == 1);
                    }
                    
                    return null;
                }
            ),
            _client
        );

        // Should have placeholder (cache is fresh)
        Assert.NotNull(query.Data);
        Assert.Equal("Cached Post", query.Data!.Title);
        Assert.True(query.IsPlaceholderData);

        // Fetch real data
        await query.ExecuteAsync();

        // Now has real data
        Assert.Equal("Fresh Post", query.Data!.Title);
        Assert.False(query.IsPlaceholderData);
    }

    public void Dispose() => _client.Dispose();

    // Helper classes
    private class BlogPost
    {
        public int Id { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Body { get; init; } = string.Empty;
    }

    private class Post
    {
        public int Id { get; init; }
        public string Title { get; init; } = string.Empty;
    }
}

