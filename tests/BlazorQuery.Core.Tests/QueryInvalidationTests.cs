namespace BlazorQuery.Core.Tests;

public class QueryInvalidationTests : IDisposable
{
    private readonly QueryClient _client = new();

    [Fact]
    public async Task InvalidateQueries_ShouldMarkQueriesAsStale()
    {
        var fetchCount = 0;
        
        var query = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("todos"),
                queryFn: async _ => {
                    fetchCount++;
                    await Task.Delay(10);
                    return $"Data {fetchCount}";
                },
                staleTime: TimeSpan.FromHours(1) // Won't refetch normally
            ),
            _client
        );

        // Initial fetch
        await query.ExecuteAsync();
        Assert.Equal(1, fetchCount);
        Assert.Equal("Data 1", query.Data);

        // Invalidate - this marks as stale and triggers refetch
        _client.InvalidateQueries(new QueryFilters
        {
            QueryKey = new QueryKey("todos")
        });

        await Task.Delay(100); // Wait for async refetch

        // Should have refetched despite long staleTime
        Assert.Equal(2, fetchCount);
        Assert.Equal("Data 2", query.Data);
    }

    [Fact]
    public async Task InvalidateQueries_WithoutFilters_ShouldInvalidateAll()
    {
        var fetchCount1 = 0;
        var fetchCount2 = 0;
        
        var query1 = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("todos"),
                queryFn: async _ => {
                    fetchCount1++;
                    await Task.Delay(10);
                    return "Todos";
                },
                staleTime: TimeSpan.FromHours(1)
            ),
            _client
        );

        var query2 = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("users"),
                queryFn: async _ => {
                    fetchCount2++;
                    await Task.Delay(10);
                    return "Users";
                },
                staleTime: TimeSpan.FromHours(1)
            ),
            _client
        );

        await query1.ExecuteAsync();
        await query2.ExecuteAsync();

        fetchCount1 = 0;
        fetchCount2 = 0;

        // Invalidate ALL queries
        _client.InvalidateQueries();

        await Task.Delay(100);

        // Both should refetch
        Assert.Equal(1, fetchCount1);
        Assert.Equal(1, fetchCount2);
    }

    [Fact]
    public async Task InvalidateQueries_PrefixMatch_ShouldMatchMultiple()
    {
        var fetchCount1 = 0;
        var fetchCount2 = 0;
        var fetchCount3 = 0;
        
        var query1 = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("todos"),
                queryFn: _ => { fetchCount1++; return Task.FromResult("Todos"); },
                staleTime: TimeSpan.FromHours(1)
            ),
            _client
        );

        var query2 = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("todos", 1),
                queryFn: _ => { fetchCount2++; return Task.FromResult("Todo 1"); },
                staleTime: TimeSpan.FromHours(1)
            ),
            _client
        );

        var query3 = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("users"),
                queryFn: _ => { fetchCount3++; return Task.FromResult("Users"); },
                staleTime: TimeSpan.FromHours(1)
            ),
            _client
        );

        await query1.ExecuteAsync();
        await query2.ExecuteAsync();
        await query3.ExecuteAsync();

        fetchCount1 = fetchCount2 = fetchCount3 = 0;

        // Invalidate only queries starting with "todos"
        _client.InvalidateQueries(new QueryFilters
        {
            QueryKey = new QueryKey("todos")
        });

        await Task.Delay(100);

        // Only todos queries should refetch
        Assert.Equal(1, fetchCount1); // todos
        Assert.Equal(1, fetchCount2); // todos, 1
        Assert.Equal(0, fetchCount3); // users (not matched)
    }

    [Fact]
    public async Task InvalidateQueries_ExactMatch_ShouldOnlyMatchExact()
    {
        var fetchCount1 = 0;
        var fetchCount2 = 0;
        
        var query1 = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("todos"),
                queryFn: _ => { fetchCount1++; return Task.FromResult("Todos"); },
                staleTime: TimeSpan.FromHours(1)
            ),
            _client
        );

        var query2 = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("todos", 1),
                queryFn: _ => { fetchCount2++; return Task.FromResult("Todo 1"); },
                staleTime: TimeSpan.FromHours(1)
            ),
            _client
        );

        await query1.ExecuteAsync();
        await query2.ExecuteAsync();

        fetchCount1 = fetchCount2 = 0;

        // Exact match only
        _client.InvalidateQueries(new QueryFilters
        {
            QueryKey = new QueryKey("todos"),
            Exact = true
        });

        await Task.Delay(100);

        // Only exact match should refetch
        Assert.Equal(1, fetchCount1); // todos (matched)
        Assert.Equal(0, fetchCount2); // todos, 1 (not matched)
    }

    [Fact]
    public async Task InvalidateQueries_WithPredicate_ShouldUseCustomLogic()
    {
        var fetchCount1 = 0;
        var fetchCount2 = 0;
        var fetchCount3 = 0;
        
        var query1 = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("todos", new { version = 5 }),
                queryFn: _ => { fetchCount1++; return Task.FromResult("Todo v5"); },
                staleTime: TimeSpan.FromHours(1)
            ),
            _client
        );

        var query2 = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("todos", new { version = 10 }),
                queryFn: _ => { fetchCount2++; return Task.FromResult("Todo v10"); },
                staleTime: TimeSpan.FromHours(1)
            ),
            _client
        );

        var query3 = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("todos", new { version = 20 }),
                queryFn: _ => { fetchCount3++; return Task.FromResult("Todo v20"); },
                staleTime: TimeSpan.FromHours(1)
            ),
            _client
        );

        await query1.ExecuteAsync();
        await query2.ExecuteAsync();
        await query3.ExecuteAsync();

        fetchCount1 = fetchCount2 = fetchCount3 = 0;

        // Custom predicate: only version >= 10
        _client.InvalidateQueries(new QueryFilters
        {
            Predicate = key => {
                if (key.Parts.Count < 2 || key.Parts[0]?.ToString() != "todos")
                    return false;
                
                // Check version from anonymous object
                var versionObj = key.Parts[1];
                if (versionObj == null) return false;
                
                var versionProp = versionObj.GetType().GetProperty("version");
                if (versionProp == null) return false;
                
                var version = (int?)versionProp.GetValue(versionObj);
                return version >= 10;
            }
        });

        await Task.Delay(100);

        // Only version >= 10 should refetch
        Assert.Equal(0, fetchCount1); // version 5 (not matched)
        Assert.Equal(1, fetchCount2); // version 10 (matched)
        Assert.Equal(1, fetchCount3); // version 20 (matched)
    }

    [Fact]
    public async Task InvalidateQueries_DisabledQuery_ShouldNotRefetch()
    {
        var fetchCount = 0;
        var options = new QueryOptions<string>(
            queryKey: new QueryKey("todos"),
            queryFn: async _ => {
                fetchCount++;
                await Task.Delay(10);
                return "Data";
            },
            enabled: true,
            staleTime: TimeSpan.FromHours(1)
        );
        
        var query = new UseQuery<string>(options, _client);

        await query.ExecuteAsync();
        Assert.Equal(1, fetchCount);

        // Disable query
        options.Enabled = false;

        // Invalidate
        _client.InvalidateQueries(new QueryFilters
        {
            QueryKey = new QueryKey("todos")
        });

        await Task.Delay(100);

        // Should NOT refetch (disabled)
        Assert.Equal(1, fetchCount);
    }

    [Fact]
    public void QueryKey_StartsWith_ShouldWorkCorrectly()
    {
        var key1 = new QueryKey("todos");
        var key2 = new QueryKey("todos", 1);
        var key3 = new QueryKey("todos", 1, "details");
        var key4 = new QueryKey("users");

        var prefix = new QueryKey("todos");

        Assert.True(key1.StartsWith(prefix));
        Assert.True(key2.StartsWith(prefix));
        Assert.True(key3.StartsWith(prefix));
        Assert.False(key4.StartsWith(prefix));

        var prefix2 = new QueryKey("todos", 1);
        Assert.False(key1.StartsWith(prefix2)); // Shorter than prefix
        Assert.True(key2.StartsWith(prefix2));
        Assert.True(key3.StartsWith(prefix2));
    }

    [Fact]
    public void QueryFilters_Matches_WithoutFilters_ShouldMatchAll()
    {
        var filters = new QueryFilters();

        Assert.True(filters.Matches(new QueryKey("todos")));
        Assert.True(filters.Matches(new QueryKey("users")));
        Assert.True(filters.Matches(new QueryKey("anything")));
    }

    [Fact]
    public void QueryFilters_Matches_PrefixMatch_ShouldWork()
    {
        var filters = new QueryFilters
        {
            QueryKey = new QueryKey("todos")
        };

        Assert.True(filters.Matches(new QueryKey("todos")));
        Assert.True(filters.Matches(new QueryKey("todos", 1)));
        Assert.False(filters.Matches(new QueryKey("users")));
    }

    [Fact]
    public void QueryFilters_Matches_ExactMatch_ShouldWork()
    {
        var filters = new QueryFilters
        {
            QueryKey = new QueryKey("todos"),
            Exact = true
        };

        Assert.True(filters.Matches(new QueryKey("todos")));
        Assert.False(filters.Matches(new QueryKey("todos", 1)));
        Assert.False(filters.Matches(new QueryKey("users")));
    }

    [Fact]
    public void QueryFilters_Matches_Predicate_ShouldWork()
    {
        var filters = new QueryFilters
        {
            Predicate = key => key.Parts.Count > 1
        };

        Assert.False(filters.Matches(new QueryKey("todos")));
        Assert.True(filters.Matches(new QueryKey("todos", 1)));
        Assert.True(filters.Matches(new QueryKey("users", 2)));
    }

    [Fact]
    public async Task InvalidateQueries_MultipleQueries_ShouldInvalidateCorrectOnes()
    {
        var queries = new List<(UseQuery<string> query, int id)>();
        if (queries == null) throw new ArgumentNullException(nameof(queries));
        var fetchCounts = new Dictionary<int, int>();

        // Create multiple queries
        for (int i = 0; i < 5; i++)
        {
            var id = i;
            fetchCounts[id] = 0;
            
            var query = new UseQuery<string>(
                new QueryOptions<string>(
                    queryKey: new QueryKey("todo", id),
                    queryFn: async _ => {
                        fetchCounts[id]++;
                        await Task.Delay(10);
                        return $"Todo {id}";
                    },
                    staleTime: TimeSpan.FromHours(1)
                ),
                _client
            );
            
            queries.Add((query, id));
            await query.ExecuteAsync();
        }

        // Reset counts
        foreach (var id in fetchCounts.Keys.ToList())
            fetchCounts[id] = 0;

        // Invalidate specific query
        _client.InvalidateQueries(new QueryFilters
        {
            QueryKey = new QueryKey("todo", 2),
            Exact = true
        });

        await Task.Delay(150);

        Assert.Equal(0, fetchCounts[0]);
        Assert.Equal(0, fetchCounts[1]);
        Assert.Equal(1, fetchCounts[2]); // Only this one
        Assert.Equal(0, fetchCounts[3]);
        Assert.Equal(0, fetchCounts[4]);
    }

    [Fact]
    public async Task InvalidateQueries_Event_ShouldContainCorrectKeys()
    {
        List<QueryKey>? capturedKeys = null;
        
        _client.OnQueriesInvalidated += keys => {
            capturedKeys = keys;
        };

        var query1 = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("todos"),
                queryFn: _ => Task.FromResult("Todos"),
                staleTime: TimeSpan.FromHours(1)
            ),
            _client
        );

        var query2 = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("todos", 1),
                queryFn: _ => Task.FromResult("Todo 1"),
                staleTime: TimeSpan.FromHours(1)
            ),
            _client
        );

        await query1.ExecuteAsync();
        await query2.ExecuteAsync();

        _client.InvalidateQueries(new QueryFilters
        {
            QueryKey = new QueryKey("todos")
        });

        await Task.Delay(50);

        Assert.NotNull(capturedKeys);
        Assert.Equal(2, capturedKeys.Count);
        Assert.Contains(new QueryKey("todos"), capturedKeys);
        Assert.Contains(new QueryKey("todos", 1), capturedKeys);
    }

    public void Dispose() => _client.Dispose();
}

