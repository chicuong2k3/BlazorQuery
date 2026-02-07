using SwrSharp.Core;

namespace SwrSharp.Core.Tests;

public class UseQueriesTests : IDisposable
{
    private readonly QueryClient _client = new();

    [Fact]
    public async Task UseQueries_ShouldExecuteMultipleQueriesInParallel()
    {
        var queries = new UseQueries<string>(_client);
        
        var queryOptions = Enumerable.Range(1, 3).Select(i =>
            new QueryOptions<string>(
                queryKey: new QueryKey("item", i),
                queryFn: async ctx => {
                    await Task.Delay(50);
                    return $"Item {i}";
                }
            )
        );
        
        queries.SetQueries(queryOptions);
        
        var startTime = DateTime.UtcNow;
        await queries.ExecuteAllAsync();
        var elapsed = DateTime.UtcNow - startTime;
        
        // Should complete in ~50ms (parallel), not ~150ms (sequential)
        Assert.True(elapsed.TotalMilliseconds < 120, $"Expected parallel execution, took {elapsed.TotalMilliseconds}ms");
        
        // All queries should succeed
        Assert.Equal(3, queries.Queries.Count);
        Assert.All(queries.Queries, q => Assert.Equal(QueryStatus.Success, q.Status));
        
        // Check data
        Assert.Equal("Item 1", queries.Queries[0].Data);
        Assert.Equal("Item 2", queries.Queries[1].Data);
        Assert.Equal("Item 3", queries.Queries[2].Data);
    }

    [Fact]
    public async Task UseQueries_ShouldHandlePartialFailures()
    {
        var queries = new UseQueries<string>(_client);
        
        var queryOptions = Enumerable.Range(1, 4).Select(i =>
            new QueryOptions<string>(
                queryKey: new QueryKey("item", i),
                queryFn: async ctx => {
                    await Task.Delay(10);
                    if (i == 2 || i == 4)
                        throw new Exception($"Failed item {i}");
                    return $"Item {i}";
                },
                retry: 0 // No retries for faster test
            )
        );
        
        queries.SetQueries(queryOptions);
        await queries.ExecuteAllAsync();
        
        // Should have 2 success and 2 errors
        var successful = queries.Queries.Where(q => q.IsSuccess).ToList();
        var failed = queries.Queries.Where(q => q.IsError).ToList();
        
        Assert.Equal(2, successful.Count);
        Assert.Equal(2, failed.Count);
        
        // Check successful items
        Assert.Contains(successful, q => q.Data == "Item 1");
        Assert.Contains(successful, q => q.Data == "Item 3");
        
        // Check failed items
        Assert.Contains(failed, q => q.Error?.Message.Contains("Failed item 2") == true);
        Assert.Contains(failed, q => q.Error?.Message.Contains("Failed item 4") == true);
    }

    [Fact]
    public async Task UseQueries_ShouldTriggerOnChangeEvent()
    {
        var queries = new UseQueries<string>(_client);
        var changeCount = 0;
        
        queries.OnChange += () => changeCount++;
        
        var queryOptions = new[]
        {
            new QueryOptions<string>(
                queryKey: new QueryKey("item1"),
                queryFn: async ctx => {
                    await Task.Delay(10);
                    return "Item 1";
                }
            )
        };
        
        queries.SetQueries(queryOptions);
        // SetQueries triggers OnChange once
        Assert.True(changeCount >= 1);
        
        var beforeExecute = changeCount;
        await queries.ExecuteAllAsync();
        
        // Should trigger OnChange during execution
        Assert.True(changeCount > beforeExecute);
    }

    [Fact]
    public async Task UseQueries_ShouldRespectQueryOptions()
    {
        var queries = new UseQueries<string>(_client);
        
        var queryOptions = new[]
        {
            new QueryOptions<string>(
                queryKey: new QueryKey("item1"),
                queryFn: async ctx => {
                    await Task.Delay(10);
                    return "Cached Item";
                },
                staleTime: TimeSpan.FromSeconds(10) // Long stale time
            )
        };
        
        queries.SetQueries(queryOptions);
        await queries.ExecuteAllAsync();
        
        Assert.Equal("Cached Item", queries.Queries[0].Data);
        
        // Execute again immediately - should use cache
        var fetchCount = 0;
        var newOptions = new[]
        {
            new QueryOptions<string>(
                queryKey: new QueryKey("item1"),
                queryFn: async ctx => {
                    fetchCount++;
                    await Task.Delay(10);
                    return "New Item";
                },
                staleTime: TimeSpan.FromSeconds(10)
            )
        };
        
        queries.SetQueries(newOptions);
        await queries.ExecuteAllAsync();
        
        // Should use cached data, not fetch again
        Assert.Equal(0, fetchCount);
        Assert.Equal("Cached Item", queries.Queries[0].Data);
    }

    [Fact]
    public async Task UseQueries_RefetchAllAsync_ShouldRefetchAllQueries()
    {
        var queries = new UseQueries<int>(_client);
        var fetchCounts = new Dictionary<int, int>();
        
        var queryOptions = Enumerable.Range(1, 3).Select(i =>
        {
            fetchCounts[i] = 0;
            return new QueryOptions<int>(
                queryKey: new QueryKey("counter", i),
                queryFn: async ctx => {
                    fetchCounts[i]++;
                    await Task.Delay(10);
                    return fetchCounts[i];
                }
            );
        });
        
        queries.SetQueries(queryOptions);
        await queries.ExecuteAllAsync();
        
        // Initial fetch
        Assert.All(queries.Queries, q => Assert.Equal(1, q.Data));
        
        // Refetch
        await queries.RefetchAllAsync();
        
        // Should increment counters
        Assert.All(queries.Queries, q => Assert.Equal(2, q.Data));
    }

    [Fact]
    public void UseQueries_SetQueries_ShouldDisposeOldQueries()
    {
        var queries = new UseQueries<string>(_client);
        
        // Set first batch
        var options1 = new[]
        {
            new QueryOptions<string>(
                queryKey: new QueryKey("old1"),
                queryFn: async ctx => "Old 1"
            )
        };
        queries.SetQueries(options1);
        var oldQuery = queries.Queries[0];
        
        // Set second batch - should dispose old queries
        var options2 = new[]
        {
            new QueryOptions<string>(
                queryKey: new QueryKey("new1"),
                queryFn: async ctx => "New 1"
            )
        };
        queries.SetQueries(options2);
        
        // New queries should be different instances
        Assert.NotSame(oldQuery, queries.Queries[0]);
        Assert.Equal(1, queries.Queries.Count);
    }

    [Fact]
    public async Task UseQueries_ShouldHandleEmptyQueryList()
    {
        var queries = new UseQueries<string>(_client);
        
        queries.SetQueries(Array.Empty<QueryOptions<string>>());
        
        await queries.ExecuteAllAsync();
        
        Assert.Empty(queries.Queries);
    }

    [Fact]
    public async Task UseQueries_ShouldSupportCancellation()
    {
        var queries = new UseQueries<string>(_client);
        var cts = new CancellationTokenSource();
        
        var queryOptions = new[]
        {
            new QueryOptions<string>(
                queryKey: new QueryKey("slow"),
                queryFn: async ctx => {
                    await Task.Delay(1000, ctx.Signal); // Long delay
                    return "Done";
                }
            )
        };
        
        queries.SetQueries(queryOptions);
        
        var executeTask = queries.ExecuteAllAsync(cts.Token);
        
        // Cancel after short delay
        await Task.Delay(50);
        cts.Cancel();
        
        // Should throw or complete quickly
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executeTask);
    }

    [Fact]
    public async Task UseQueries_WithRetry_ShouldRetryFailedQueries()
    {
        var queries = new UseQueries<string>(_client);
        var attemptCounts = new Dictionary<int, int>();
        
        var queryOptions = Enumerable.Range(1, 2).Select(i =>
        {
            attemptCounts[i] = 0;
            return new QueryOptions<string>(
                queryKey: new QueryKey("item", i),
                queryFn: async ctx => {
                    attemptCounts[i]++;
                    await Task.Delay(10);
                    
                    // Fail first 2 attempts, succeed on 3rd
                    if (attemptCounts[i] < 3)
                        throw new Exception($"Attempt {attemptCounts[i]}");
                    
                    return $"Item {i}";
                },
                retry: 3 // 3 retries after initial = 4 total attempts
            );
        });
        
        queries.SetQueries(queryOptions);
        await queries.ExecuteAllAsync();
        
        // Should succeed after retries
        Assert.All(queries.Queries, q => Assert.Equal(QueryStatus.Success, q.Status));
        Assert.All(queries.Queries, q => Assert.NotNull(q.Data));
        
        // Each query should have attempted 3 times
        Assert.All(attemptCounts.Values, count => Assert.Equal(3, count));
    }

    [Fact]
    public async Task UseQueries_WithDifferentStaleTime_ShouldRespectIndividualSettings()
    {
        var queries = new UseQueries<string>(_client);
        
        // Query 1: Fresh data (long stale time)
        // Query 2: Stale data (short stale time)
        var queryOptions = new[]
        {
            new QueryOptions<string>(
                queryKey: new QueryKey("fresh"),
                queryFn: async ctx => "Fresh Data",
                staleTime: TimeSpan.FromHours(1)
            ),
            new QueryOptions<string>(
                queryKey: new QueryKey("stale"),
                queryFn: async ctx => "Stale Data",
                staleTime: TimeSpan.FromMilliseconds(1)
            )
        };
        
        queries.SetQueries(queryOptions);
        await queries.ExecuteAllAsync();
        
        // Wait for stale query to become stale
        await Task.Delay(10);
        
        var fetchCount = 0;
        var newOptions = new[]
        {
            new QueryOptions<string>(
                queryKey: new QueryKey("fresh"),
                queryFn: async ctx => {
                    fetchCount++;
                    return "Fresh Data Updated";
                },
                staleTime: TimeSpan.FromHours(1)
            ),
            new QueryOptions<string>(
                queryKey: new QueryKey("stale"),
                queryFn: async ctx => {
                    fetchCount++;
                    return "Stale Data Updated";
                },
                staleTime: TimeSpan.FromMilliseconds(1)
            )
        };
        
        queries.SetQueries(newOptions);
        await queries.ExecuteAllAsync();
        
        // Only stale query should refetch (fresh is cached)
        Assert.Equal(1, fetchCount);
        Assert.Equal("Fresh Data", queries.Queries[0].Data); // Cached
        Assert.Equal("Stale Data Updated", queries.Queries[1].Data); // Refetched
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}

