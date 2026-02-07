namespace SwrSharp.Core.Tests;

public class DefaultQueryFunctionTests : IDisposable
{
    [Fact]
    public async Task DefaultQueryFn_ShouldBeUsedWhenNoQueryFnProvided()
    {
        var client = new QueryClient();
        client.SetDefaultQueryFn<string>(ctx => {
            var key = ctx.QueryKey.Parts[0]?.ToString();
            return Task.FromResult(key == "data" ? "Default Data" : "Unknown");
        });

        var query = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("data")
                // No queryFn provided - will use default
            ),
            client
        );

        await query.ExecuteAsync();

        Assert.Equal("Default Data", query.Data);
        Assert.True(query.IsSuccess);
    }

    [Fact]
    public async Task DefaultQueryFn_ShouldUseQueryKeyToDetermineFetch()
    {
        var client = new QueryClient();
        client.SetDefaultQueryFn<List<string>>(ctx => {
            // Use query key to determine what to fetch
            var endpoint = ctx.QueryKey.Parts[0]?.ToString();
            return Task.FromResult(endpoint switch
            {
                "/posts" => new List<string> { "Post 1", "Post 2" },
                "/users" => new List<string> { "User 1", "User 2" },
                _ => new List<string>()
            });
        });

        var postsQuery = new UseQuery<List<string>>(
            new QueryOptions<List<string>>(
                queryKey: new QueryKey("/posts")
            ),
            client
        );

        var usersQuery = new UseQuery<List<string>>(
            new QueryOptions<List<string>>(
                queryKey: new QueryKey("/users")
            ),
            client
        );

        await postsQuery.ExecuteAsync();
        await usersQuery.ExecuteAsync();

        Assert.Equal(2, postsQuery.Data?.Count);
        if (postsQuery.Data != null)
        {
            Assert.Contains("Post 1", postsQuery.Data);

            Assert.Equal(2, usersQuery.Data?.Count);
            if (usersQuery.Data != null) Assert.Contains("User 1", usersQuery.Data);
        }
    }

    [Fact]
    public async Task ProvidedQueryFn_ShouldOverrideDefaultQueryFn()
    {
        var client = new QueryClient();
        client.SetDefaultQueryFn<string>(_ => Task.FromResult("Default Data"));

        var query = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("data"),
                queryFn: _ => Task.FromResult("Custom Data") // Override default
            ),
            client
        );

        await query.ExecuteAsync();

        Assert.Equal("Custom Data", query.Data);
    }

    [Fact]
    public void NoQueryFn_AndNoDefaultQueryFn_ShouldThrowException()
    {
        // No default query function set
        var client = new QueryClient();

        Assert.Throws<InvalidOperationException>(() =>
        {
            new UseQuery<string>(
                new QueryOptions<string>(queryKey: new QueryKey("data")),
                client
            );
        });
    }

    [Fact]
    public async Task DefaultQueryFn_WithComplexQueryKey_ShouldWork()
    {
        var client = new QueryClient();
        client.SetDefaultQueryFn<string>(ctx => {
            var endpoint = ctx.QueryKey.Parts[0]?.ToString();
            var id = ctx.QueryKey.Parts.Count > 1 ? ctx.QueryKey.Parts[1] : null;

            if (endpoint == "/posts" && id is int postId)
            {
                return Task.FromResult($"Post {postId}");
            }

            return Task.FromResult("Unknown");
        });

        var query = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("/posts", 123)
            ),
            client
        );

        await query.ExecuteAsync();

        Assert.Equal("Post 123", query.Data);
    }

    [Fact]
    public async Task DefaultQueryFn_WithDifferentTypes_ShouldWork()
    {
        var client = new QueryClient();

        // Register type-specific default functions for each type
        client.SetDefaultQueryFn<string>(_ => Task.FromResult("String Data"));
        client.SetDefaultQueryFn<int>(_ => Task.FromResult(42));
        client.SetDefaultQueryFn<List<int>>(_ => Task.FromResult(new List<int> { 1, 2, 3 }));

        var stringQuery = new UseQuery<string>(
            new QueryOptions<string>(queryKey: new QueryKey("string")),
            client
        );

        var intQuery = new UseQuery<int>(
            new QueryOptions<int>(queryKey: new QueryKey("int")),
            client
        );

        var listQuery = new UseQuery<List<int>>(
            new QueryOptions<List<int>>(queryKey: new QueryKey("list")),
            client
        );

        await stringQuery.ExecuteAsync();
        await intQuery.ExecuteAsync();
        await listQuery.ExecuteAsync();

        Assert.Equal("String Data", stringQuery.Data);
        Assert.Equal(42, intQuery.Data);
        Assert.Equal(3, listQuery.Data?.Count);
    }

    [Fact]
    public void DefaultQueryFn_ForOneType_ShouldNotAffectOtherTypes()
    {
        var client = new QueryClient();
        // Only register default for string type
        client.SetDefaultQueryFn<string>(_ => Task.FromResult("String Data"));

        // Should throw for int since no default registered for int
        Assert.Throws<InvalidOperationException>(() =>
        {
            new UseQuery<int>(
                new QueryOptions<int>(queryKey: new QueryKey("data")),
                client
            );
        });
    }

    [Fact]
    public async Task DefaultQueryFn_WithEnabledOption_ShouldWork()
    {
        var fetchCount = 0;

        var client = new QueryClient();
        client.SetDefaultQueryFn<string>(_ =>
        {
            fetchCount++;
            return Task.FromResult("Data");
        });

        var query = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("data"),
                enabled: false // Disabled
            ),
            client
        );

        await query.ExecuteAsync();

        Assert.Equal(0, fetchCount); // Should not fetch when disabled
    }

    [Fact]
    public async Task DefaultQueryFn_WithStaleTime_ShouldRespectCaching()
    {
        var fetchCount = 0;

        var client = new QueryClient();
        client.SetDefaultQueryFn<string>(_ =>
        {
            fetchCount++;
            return Task.FromResult("Data");
        });

        var query1 = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("data"),
                staleTime: TimeSpan.FromHours(1)
            ),
            client
        );

        await query1.ExecuteAsync();
        Assert.Equal(1, fetchCount);

        // Create same query - should use cache
        var query2 = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("data"),
                staleTime: TimeSpan.FromHours(1)
            ),
            client
        );

        await query2.ExecuteAsync();
        Assert.Equal(1, fetchCount); // Should not fetch again (cached)
    }

    [Fact]
    public async Task DefaultQueryFn_Documentation_Example()
    {
        // Example from documentation: API endpoint based on query key
        var client = new QueryClient();
        client.SetDefaultQueryFn<List<Post>>(async ctx => {
            var endpoint = ctx.QueryKey.Parts[0]?.ToString();
            // Simulate API call based on endpoint
            await Task.Delay(10);

            return endpoint switch
            {
                "/posts" => new List<Post> {
                    new() { Title = "Post 1" },
                    new() { Title = "Post 2" }
                },
                _ => new List<Post>()
            };
        });

        // All you have to do now is pass a key!
        var query = new UseQuery<List<Post>>(
            new QueryOptions<List<Post>>(
                queryKey: new QueryKey("/posts")
                // No queryFn needed!
            ),
            client
        );

        await query.ExecuteAsync();

        Assert.Equal(2, query.Data?.Count);
        Assert.Equal("Post 1", query.Data?[0].Title);
    }

    [Fact]
    public async Task DefaultQueryFn_WithAnonymousObjectInKey_ShouldWork()
    {
        var client = new QueryClient();
        client.SetDefaultQueryFn<List<string>>(ctx => {
            var endpoint = ctx.QueryKey.Parts[0]?.ToString();
            var filters = ctx.QueryKey.Parts.Count > 1 ? ctx.QueryKey.Parts[1] : null;

            if (endpoint == "/posts" && filters != null)
            {
                var typeProp = filters.GetType().GetProperty("type");
                var type = typeProp?.GetValue(filters)?.ToString();

                return Task.FromResult(type == "featured"
                    ? new List<string> { "Featured Post 1", "Featured Post 2" }
                    : new List<string> { "Regular Post 1" });
            }

            return Task.FromResult(new List<string>());
        });

        var query = new UseQuery<List<string>>(
            new QueryOptions<List<string>>(
                queryKey: new QueryKey("/posts", new { type = "featured" })
            ),
            client
        );

        await query.ExecuteAsync();

        Assert.Equal(2, query.Data?.Count);
        if (query.Data != null)
            if (query != null)
                Assert.Contains("Featured Post 1", query.Data);
    }

    public void Dispose()
    {
        // Cleanup if needed
    }

    // Helper classes
    private class Post
    {
        public string Title { get; init; } = string.Empty;
    }
}


