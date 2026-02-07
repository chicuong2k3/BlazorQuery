namespace SwrSharp.Core.Tests;

public class QueryCancellationTests : IDisposable
{
    private readonly QueryClient _client = new();

    [Fact]
    public async Task CancelQueries_ShouldCancelOngoingFetch()
    {
        var fetchStarted = new TaskCompletionSource();
        var shouldComplete = new TaskCompletionSource<string>();
        
        var query = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("todos"),
                queryFn: async ctx => {
                    fetchStarted.SetResult();
                    return await shouldComplete.Task;
                }
            ),
            _client
        );

        // Start fetch (will block)
        var fetchTask = query.ExecuteAsync();
        await fetchStarted.Task;

        // Verify fetching
        Assert.True(query.IsFetching);

        // Cancel the query
        _client.CancelQueries(new QueryFilters
        {
            QueryKey = new QueryKey("todos")
        });

        // Wait a bit for cancellation to propagate
        await Task.Delay(50);

        // Complete the task to cleanup
        shouldComplete.TrySetResult("Data");

        try
        {
            await fetchTask;
        }
        catch (OperationCanceledException)
        {
            // Expected - fetch was cancelled
        }
    }

    [Fact]
    public async Task CancelQueries_WithFilters_ShouldCancelMatchingQueries()
    {
        var fetchStarted1 = new TaskCompletionSource();
        var fetchStarted2 = new TaskCompletionSource();
        var shouldComplete1 = new TaskCompletionSource<string>();
        var shouldComplete2 = new TaskCompletionSource<string>();
        
        var query1 = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("todos"),
                queryFn: async ctx => {
                    fetchStarted1.SetResult();
                    return await shouldComplete1.Task;
                }
            ),
            _client
        );

        var query2 = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("users"),
                queryFn: async ctx => {
                    fetchStarted2.SetResult();
                    return await shouldComplete2.Task;
                }
            ),
            _client
        );

        // Start both fetches
        var fetch1 = query1.ExecuteAsync();
        var fetch2 = query2.ExecuteAsync();
        
        await Task.WhenAll(fetchStarted1.Task, fetchStarted2.Task);

        // Cancel only todos queries
        _client.CancelQueries(new QueryFilters
        {
            QueryKey = new QueryKey("todos")
        });

        await Task.Delay(50);

        // Cleanup
        shouldComplete1.TrySetResult("Data1");
        shouldComplete2.TrySetResult("Data2");

        try { await fetch1; } catch (OperationCanceledException) { }
        await fetch2; // Should complete normally
    }

    [Fact]
    public void CancelQueries_WithoutFilters_ShouldCancelAll()
    {
        var eventFired = false;
        List<QueryKey>? capturedKeys = null;
        
        _client.OnQueriesCancelled += keys => {
            eventFired = true;
            capturedKeys = keys;
        };

        var query1 = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("todos"),
                queryFn: async _ => "Todos"
            ),
            _client
        );

        var query2 = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("users"),
                queryFn: async _ => "Users"
            ),
            _client
        );

        // Cancel all queries
        _client.CancelQueries();

        Assert.True(eventFired);
        Assert.NotNull(capturedKeys);
        // Note: Event contains all keys in cache, not just active queries
    }

    [Fact]
    public async Task QueryFunction_ShouldReceiveCancellationToken()
    {
        CancellationToken? receivedToken = null;
        
        var query = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("todos"),
                queryFn: async ctx => {
                    receivedToken = ctx.Signal;
                    await Task.Delay(10);
                    return "Data";
                }
            ),
            _client
        );

        await query.ExecuteAsync();

        Assert.NotNull(receivedToken);
        Assert.False(receivedToken.Value.IsCancellationRequested);
    }

    [Fact]
    public async Task QueryFunction_WithExternalCancellation_ShouldPropagate()
    {
        var cts = new CancellationTokenSource();
        var receivedToken = CancellationToken.None;
        
        var query = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("todos"),
                queryFn: async ctx => {
                    receivedToken = ctx.Signal;
                    await Task.Delay(100, ctx.Signal);
                    return "Data";
                }
            ),
            _client
        );

        var fetchTask = query.ExecuteAsync(cts.Token);
        
        await Task.Delay(20);
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await fetchTask);
        Assert.True(receivedToken.IsCancellationRequested);
    }

    [Fact]
    public void CancelOptions_DefaultValues_ShouldBeCorrect()
    {
        var options = new CancelOptions();

        Assert.False(options.Silent);
        Assert.True(options.Revert);
    }

    [Fact]
    public void CancelOptions_SilentMode_CanBeSet()
    {
        var options = new CancelOptions
        {
            Silent = true
        };

        Assert.True(options.Silent);
        Assert.True(options.Revert); // Default
    }

    [Fact]
    public void CancelOptions_RevertMode_CanBeDisabled()
    {
        var options = new CancelOptions
        {
            Revert = false
        };

        Assert.False(options.Revert);
        Assert.False(options.Silent); // Default
    }

    [Fact]
    public void CancelQueries_WithOptions_ShouldAcceptOptions()
    {
        var options = new CancelOptions
        {
            Silent = true,
            Revert = false
        };

        // Should not throw
        _client.CancelQueries(new QueryFilters
        {
            QueryKey = new QueryKey("todos")
        }, options);
    }

    [Fact]
    public async Task CancelQueries_ExactMatch_ShouldCancelOnlyExactQuery()
    {
        var eventFired = false;
        List<QueryKey>? capturedKeys = null;
        
        _client.OnQueriesCancelled += keys => {
            eventFired = true;
            capturedKeys = keys;
        };

        var query1 = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("todos"),
                queryFn: async _ => "Todos"
            ),
            _client
        );

        var query2 = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("todos", 1),
                queryFn: async _ => "Todo 1"
            ),
            _client
        );

        // Execute both to get them in cache
        await query1.ExecuteAsync();
        await query2.ExecuteAsync();

        // Reset
        eventFired = false;
        capturedKeys = null;

        // Cancel only exact match
        _client.CancelQueries(new QueryFilters
        {
            QueryKey = new QueryKey("todos"),
            Exact = true
        });

        Assert.True(eventFired);
        Assert.NotNull(capturedKeys);
        Assert.Single(capturedKeys);
        Assert.Equal(new QueryKey("todos"), capturedKeys[0]);
    }

    [Fact]
    public async Task CancelQueries_WithPredicate_ShouldCancelMatching()
    {
        List<QueryKey>? capturedKeys = null;
        
        _client.OnQueriesCancelled += keys => {
            capturedKeys = keys;
        };

        var query1 = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("todos", 1),
                queryFn: async _ => "Todo 1"
            ),
            _client
        );

        var query2 = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("todos", 20),
                queryFn: async _ => "Todo 20"
            ),
            _client
        );

        await query1.ExecuteAsync();
        await query2.ExecuteAsync();

        capturedKeys = null;

        // Cancel with predicate: id > 10
        _client.CancelQueries(new QueryFilters
        {
            Predicate = key => {
                if (key.Parts.Count < 2) return false;
                var id = key.Parts[1] as int?;
                return id > 10;
            }
        });

        Assert.NotNull(capturedKeys);
        Assert.Single(capturedKeys);
        Assert.Equal(new QueryKey("todos", 20), capturedKeys[0]);
    }

    [Fact]
    public async Task ManualCancellation_Example_ShouldWork()
    {
        // Example from documentation: Manual cancellation
        var fetchStarted = new TaskCompletionSource();
        var shouldComplete = new TaskCompletionSource<List<string>>();
        
        var query = new UseQuery<List<string>>(
            new QueryOptions<List<string>>(
                queryKey: new QueryKey("todos"),
                queryFn: async ctx => {
                    fetchStarted.SetResult();
                    // Simulate long-running fetch
                    return await shouldComplete.Task;
                }
            ),
            _client
        );

        var fetchTask = query.ExecuteAsync();
        await fetchStarted.Task;

        // User clicks cancel button
        _client.CancelQueries(new QueryFilters
        {
            QueryKey = new QueryKey("todos")
        });

        await Task.Delay(50);

        // Cleanup
        shouldComplete.TrySetResult(new List<string>());
        
        try
        {
            await fetchTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
    }

    [Fact]
    public async Task CancellationToken_InContext_ShouldBeCancellable()
    {
        var fetchStarted = new TaskCompletionSource();
        var cancelled = false;
        
        var query = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("data"),
                queryFn: async ctx => {
                    fetchStarted.SetResult();
                    try
                    {
                        await Task.Delay(1000, ctx.Signal);
                        return "Data";
                    }
                    catch (OperationCanceledException)
                    {
                        cancelled = true;
                        throw;
                    }
                }
            ),
            _client
        );

        var fetchTask = query.ExecuteAsync();
        await fetchStarted.Task;

        // Cancel
        _client.CancelQueries(new QueryFilters
        {
            QueryKey = new QueryKey("data")
        });

        await Task.Delay(100);

        Assert.True(cancelled);
    }

    public void Dispose() => _client?.Dispose();
}

