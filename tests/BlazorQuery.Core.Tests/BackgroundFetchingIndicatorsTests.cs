namespace BlazorQuery.Core.Tests;

public class BackgroundFetchingIndicatorsTests : IDisposable
{
    private readonly QueryClient _client = new();

    [Fact]
    public async Task IsFetching_ShouldBeTrueWhenQueryIsFetching()
    {
        var query = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("test"),
                queryFn: async _ => {
                    await Task.Delay(100);
                    return "Data";
                }
            ),
            _client
        );

        // Before fetch
        Assert.False(query.IsFetching);
        Assert.Equal(FetchStatus.Idle, query.FetchStatus);

        // Start fetch (don't await)
        var fetchTask = query.ExecuteAsync();
        await Task.Delay(20); // Let it start

        // During fetch
        Assert.True(query.IsFetching);
        Assert.Equal(FetchStatus.Fetching, query.FetchStatus);

        // Wait for completion
        await fetchTask;

        // After fetch
        Assert.False(query.IsFetching);
        Assert.Equal(FetchStatus.Idle, query.FetchStatus);
    }

    [Fact]
    public async Task IsFetchingBackground_ShouldBeTrueWhenRefetchingWithStaleData()
    {
        var query = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("test"),
                queryFn: async _ => {
                    await Task.Delay(50);
                    return "Data";
                },
                staleTime: TimeSpan.FromMilliseconds(1) // Very short stale time
            ),
            _client
        );

        // Initial fetch
        await query.ExecuteAsync();
        Assert.False(query.IsFetchingBackground);
        Assert.Equal("Data", query.Data);

        // Wait for data to become stale
        await Task.Delay(10);

        // Background refetch
        var refetchTask = query.ExecuteAsync();
        await Task.Delay(20); // Let it start

        // Should be background fetching (has old data while fetching new)
        Assert.True(query.IsFetchingBackground);
        Assert.True(query.IsFetching);
        Assert.Equal("Data", query.Data); // Old data still available

        await refetchTask;

        // After refetch
        Assert.False(query.IsFetchingBackground);
        Assert.False(query.IsFetching);
    }

    [Fact]
    public Task GlobalIsFetching_ShouldBeFalseInitially()
    {
        Assert.False(_client.IsFetching);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GlobalIsFetching_ShouldBeTrueWhenAnyQueryIsFetching()
    {
        var query1 = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("query1"),
                queryFn: async _ => {
                    await Task.Delay(100);
                    return "Data 1";
                }
            ),
            _client
        );

        var query2 = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("query2"),
                queryFn: async _ => {
                    await Task.Delay(100);
                    return "Data 2";
                }
            ),
            _client
        );

        // Initially false
        Assert.False(_client.IsFetching);

        // Start first query
        var task1 = query1.ExecuteAsync();
        await Task.Delay(20);

        // Should be true (one query fetching)
        Assert.True(_client.IsFetching);

        // Start second query
        var task2 = query2.ExecuteAsync();
        await Task.Delay(20);

        // Still true (two queries fetching)
        Assert.True(_client.IsFetching);

        // Wait for first to complete
        await task1;

        // Still true (second still fetching)
        Assert.True(_client.IsFetching);

        // Wait for second to complete
        await task2;

        // Now false (no queries fetching)
        Assert.False(_client.IsFetching);
    }

    [Fact]
    public async Task GlobalIsFetching_ShouldHandleMultipleQueriesInParallel()
    {
        var queries = Enumerable.Range(1, 5).Select(i =>
            new UseQuery<string>(
                new QueryOptions<string>(
                    queryKey: new QueryKey("query", i),
                    queryFn: async _ => {
                        await Task.Delay(50);
                        return $"Data {i}";
                    }
                ),
                _client
            )
        ).ToList();

        Assert.False(_client.IsFetching);

        // Start all queries in parallel
        var tasks = queries.Select(q => q.ExecuteAsync()).ToList();
        await Task.Delay(20); // Let them start

        // Should be true (multiple queries fetching)
        Assert.True(_client.IsFetching);

        // Wait for all to complete
        await Task.WhenAll(tasks);

        // Should be false
        Assert.False(_client.IsFetching);
    }

    [Fact]
    public async Task GlobalIsFetching_OnFetchingChanged_ShouldFireWhenStateChanges()
    {
        var changeEvents = new List<bool>();
        
        _client.OnFetchingChanged += () => {
            changeEvents.Add(_client.IsFetching);
        };

        var query = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("test"),
                queryFn: async _ => {
                    await Task.Delay(50);
                    return "Data";
                }
            ),
            _client
        );

        await query.ExecuteAsync();

        // Should have fired: false->true (start), true->false (complete)
        Assert.Equal(2, changeEvents.Count);
        Assert.True(changeEvents[0]); // First event: started fetching
        Assert.False(changeEvents[1]); // Second event: stopped fetching
    }

    [Fact]
    public async Task GlobalIsFetching_ShouldNotChangeWhenStillFetchingOtherQueries()
    {
        var changeCount = 0;
        
        _client.OnFetchingChanged += () => {
            changeCount++;
        };

        var query1 = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("query1"),
                queryFn: async _ => {
                    await Task.Delay(100);
                    return "Data 1";
                }
            ),
            _client
        );

        var query2 = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("query2"),
                queryFn: async _ => {
                    await Task.Delay(150);
                    return "Data 2";
                }
            ),
            _client
        );

        // Start both
        var task1 = query1.ExecuteAsync();
        var task2 = query2.ExecuteAsync();
        
        await Task.Delay(20); // Let them start
        var afterStart = changeCount;

        // Wait for first to complete (but second still running)
        await task1;
        var afterFirstComplete = changeCount;

        // Should not have fired when first completed (second still running)
        Assert.Equal(afterStart, afterFirstComplete);

        // Wait for second
        await task2;
        var afterBothComplete = changeCount;

        // Should have fired when second completed
        Assert.True(afterBothComplete > afterFirstComplete);

        // Total: 1 on start, 1 on all complete = 2
        Assert.Equal(2, changeCount);
    }

    [Fact]
    public async Task GlobalIsFetching_WithRefetch_ShouldWork()
    {
        var query = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("test"),
                queryFn: async _ => {
                    await Task.Delay(50);
                    return "Data";
                }
            ),
            _client
        );

        // Initial fetch
        await query.ExecuteAsync();
        Assert.False(_client.IsFetching);

        // Refetch
        var refetchTask = query.RefetchAsync();
        await Task.Delay(20);

        Assert.True(_client.IsFetching);

        await refetchTask;
        Assert.False(_client.IsFetching);
    }

    [Fact]
    public async Task GlobalIsFetching_WithFailedQuery_ShouldStillDecrementCorrectly()
    {
        var query = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("test"),
                queryFn: async _ => {
                    await Task.Delay(50);
                    throw new Exception("Fetch failed");
                },
                retry: 0 // No retries
            ),
            _client
        );

        Assert.False(_client.IsFetching);

        // Execute (will fail)
        await query.ExecuteAsync();

        // Should still be false after failure
        Assert.False(_client.IsFetching);
    }

    [Fact]
    public async Task GlobalIsFetching_WithDisabledQuery_ShouldNotIncrement()
    {
        var query = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("test"),
                queryFn: async _ => {
                    await Task.Delay(50);
                    return "Data";
                },
                enabled: false
            ),
            _client
        );

        await query.ExecuteAsync();

        // Should remain false (query didn't execute)
        Assert.False(_client.IsFetching);
    }

    [Fact]
    public async Task GlobalIsFetching_WithUseQueries_ShouldTrackAllQueries()
    {
        var queries = new UseQueries<string>(_client);
        
        var queryOptions = Enumerable.Range(1, 3).Select(i =>
            new QueryOptions<string>(
                queryKey: new QueryKey("item", i),
                queryFn: async _ => {
                    await Task.Delay(50);
                    return $"Item {i}";
                }
            )
        );

        queries.SetQueries(queryOptions);

        Assert.False(_client.IsFetching);

        var executeTask = queries.ExecuteAllAsync();
        await Task.Delay(20);

        // Should be true (all 3 queries fetching)
        Assert.True(_client.IsFetching);

        await executeTask;

        // Should be false (all complete)
        Assert.False(_client.IsFetching);
    }

    public void Dispose() => _client?.Dispose();
}

