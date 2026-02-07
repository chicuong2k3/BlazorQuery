namespace BlazorQuery.Core.Tests;

public class DependentQueriesTests : IDisposable
{
    private readonly QueryClient _client = new();

    [Fact]
    public async Task DependentQuery_ShouldNotExecuteWhenDisabled()
    {
        var fetchCount = 0;
        
        var query = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("dependent"),
                queryFn: async ctx => {
                    fetchCount++;
                    await Task.Delay(10);
                    return "Data";
                },
                enabled: false // Disabled initially
            ),
            _client
        );

        await query.ExecuteAsync();

        // Should not execute
        Assert.Equal(0, fetchCount);
        Assert.Equal(QueryStatus.Pending, query.Status);
        Assert.Equal(FetchStatus.Idle, query.FetchStatus);
        Assert.Null(query.Data);
    }

    [Fact]
    public async Task DependentQuery_ShouldExecuteWhenEnabled()
    {
        var fetchCount = 0;
        
        var options = new QueryOptions<string>(
            queryKey: new QueryKey("dependent"),
            queryFn: async ctx => {
                fetchCount++;
                await Task.Delay(10);
                return "Data";
            },
            enabled: true
        );

        var query = new UseQuery<string>(options, _client);
        await query.ExecuteAsync();

        Assert.Equal(1, fetchCount);
        Assert.Equal(QueryStatus.Success, query.Status);
        Assert.Equal("Data", query.Data);
    }

    [Fact]
    public async Task DependentQuery_ShouldTransitionFromIdleToPendingToSuccess()
    {
        string? userId = null;
        var userFetchCount = 0;
        var projectsFetchCount = 0;

        // First query - get user
        var userQuery = new UseQuery<User>(
            new QueryOptions<User>(
                queryKey: new QueryKey("user", "email@test.com"),
                queryFn: async ctx => {
                    userFetchCount++;
                    await Task.Delay(50);
                    return new User { Id = "123", Email = "email@test.com" };
                }
            ),
            _client
        );

        // Second query - depends on user
        var projectsOptions = new QueryOptions<List<string>>(
            queryKey: new QueryKey("projects", userId),
            queryFn: async ctx => {
                projectsFetchCount++;
                await Task.Delay(50);
                return new List<string> { "Project 1", "Project 2" };
            },
            enabled: !string.IsNullOrEmpty(userId)
        );

        var projectsQuery = new UseQuery<List<string>>(projectsOptions, _client);

        // Step 1: Projects query is disabled (userId is null)
        await projectsQuery.ExecuteAsync();
        
        Assert.Equal(0, projectsFetchCount);
        Assert.Equal(QueryStatus.Pending, projectsQuery.Status);
        Assert.Equal(FetchStatus.Idle, projectsQuery.FetchStatus);
        Assert.True(projectsQuery.IsPending);

        // Step 2: Fetch user
        await userQuery.ExecuteAsync();
        userId = userQuery.Data?.Id;
        
        Assert.Equal(1, userFetchCount);
        Assert.NotNull(userId);

        // Step 3: Now enable and execute projects query
        projectsOptions.Enabled = !string.IsNullOrEmpty(userId);
        
        Assert.Equal(QueryStatus.Pending, projectsQuery.Status);
        Assert.True(projectsQuery.IsPending);
        Assert.Equal(FetchStatus.Idle, projectsQuery.FetchStatus);

        await projectsQuery.ExecuteAsync();
        
        // Should transition to fetching
        Assert.Equal(1, projectsFetchCount);
        Assert.Equal(QueryStatus.Success, projectsQuery.Status);
        Assert.False(projectsQuery.IsPending);
        Assert.Equal(FetchStatus.Idle, projectsQuery.FetchStatus);
        Assert.NotNull(projectsQuery.Data);
    }

    [Fact]
    public async Task DependentQuery_RealWorldExample_UserThenProjects()
    {
        // Simulate real-world scenario: fetch user by email, then fetch their projects

        // Step 1: Fetch user
        var userQuery = new UseQuery<User>(
            new QueryOptions<User>(
                queryKey: new QueryKey("user", "john@example.com"),
                queryFn: async ctx => {
                    await Task.Delay(50);
                    return new User 
                    { 
                        Id = "user-123", 
                        Email = "john@example.com",
                        Name = "John Doe"
                    };
                }
            ),
            _client
        );

        await userQuery.ExecuteAsync();
        
        Assert.True(userQuery.IsSuccess);
        var userId = userQuery.Data!.Id;

        // Step 2: Fetch projects (dependent on userId)
        var projectsQuery = new UseQuery<List<Project>>(
            new QueryOptions<List<Project>>(
                queryKey: new QueryKey("projects", userId),
                queryFn: async ctx => {
                    var (queryKey, signal) = ctx;
                    var uid = (string)queryKey[1]!;
                    await Task.Delay(50);
                    
                    return new List<Project>
                    {
                        new() { Id = "proj-1", UserId = uid, Name = "Project Alpha" },
                        new() { Id = "proj-2", UserId = uid, Name = "Project Beta" }
                    };
                },
                enabled: !string.IsNullOrEmpty(userId)
            ),
            _client
        );

        await projectsQuery.ExecuteAsync();
        
        Assert.True(projectsQuery.IsSuccess);
        Assert.Equal(2, projectsQuery.Data!.Count);
        Assert.All(projectsQuery.Data, p => Assert.Equal(userId, p.UserId));
    }

    [Fact]
    public async Task DependentQueries_WithUseQueries_ShouldWorkCorrectly()
    {
        // Step 1: Fetch user IDs
        var usersQuery = new UseQuery<List<string>>(
            new QueryOptions<List<string>>(
                queryKey: new QueryKey("users"),
                queryFn: async ctx => {
                    await Task.Delay(50);
                    return new List<string> { "user1", "user2", "user3" };
                }
            ),
            _client
        );

        await usersQuery.ExecuteAsync();
        var userIds = usersQuery.Data;

        Assert.NotNull(userIds);
        Assert.Equal(3, userIds.Count);

        // Step 2: Fetch messages for each user (dependent queries)
        var queries = new UseQueries<List<string>>(_client);
        
        var messageQueries = userIds!.Select(userId =>
            new QueryOptions<List<string>>(
                queryKey: new QueryKey("messages", userId),
                queryFn: async ctx => {
                    var (queryKey, signal) = ctx;
                    var uid = (string)queryKey[1]!;
                    await Task.Delay(30);
                    return new List<string> 
                    { 
                        $"Message 1 from {uid}",
                        $"Message 2 from {uid}"
                    };
                },
                enabled: true // Enabled because userIds is available
            )
        );

        queries.SetQueries(messageQueries);
        await queries.ExecuteAllAsync();

        // All queries should succeed
        Assert.Equal(3, queries.Queries.Count);
        Assert.All(queries.Queries, q => Assert.True(q.IsSuccess));
        Assert.All(queries.Queries, q => Assert.Equal(2, q.Data!.Count));
    }

    [Fact]
    public async Task DependentQueries_WithUseQueries_EmptyWhenDisabled()
    {
        // When dependency is not available, return empty array
        List<string>? userIds = null;

        var queries = new UseQueries<List<string>>(_client);
        
        // If userIds is null, pass empty array
        var messageQueries = userIds != null
            ? userIds.Select(userId =>
                new QueryOptions<List<string>>(
                    queryKey: new QueryKey("messages", userId),
                    queryFn: async ctx => {
                        await Task.Delay(10);
                        return new List<string> { "Message" };
                    }
                ))
            : Enumerable.Empty<QueryOptions<List<string>>>();

        queries.SetQueries(messageQueries);
        await queries.ExecuteAllAsync();

        // Should be empty
        Assert.Empty(queries.Queries);
    }

    [Fact]
    public async Task DependentQuery_ShouldNotRefetchWhenStillDisabled()
    {
        var fetchCount = 0;
        
        var query = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("dependent"),
                queryFn: async ctx => {
                    fetchCount++;
                    await Task.Delay(10);
                    return "Data";
                },
                enabled: false
            ),
            _client
        );

        // Multiple execute attempts
        await query.ExecuteAsync();
        await query.ExecuteAsync();
        await query.RefetchAsync();

        // Should never execute
        Assert.Equal(0, fetchCount);
    }

    [Fact]
    public async Task DependentQuery_CanBeReEnabled()
    {
        var fetchCount = 0;
        
        var options = new QueryOptions<string>(
            queryKey: new QueryKey("dependent"),
            queryFn: async ctx => {
                fetchCount++;
                await Task.Delay(10);
                return $"Data {fetchCount}";
            },
            enabled: false
        );

        var query = new UseQuery<string>(options, _client);

        // Disabled - should not execute
        await query.ExecuteAsync();
        Assert.Equal(0, fetchCount);

        // Enable and execute
        options.Enabled = true;
        await query.ExecuteAsync();
        
        Assert.Equal(1, fetchCount);
        Assert.Equal("Data 1", query.Data);
        Assert.True(query.IsSuccess);
    }

    [Fact]
    public async Task DependentQuery_WithStaleTime_ShouldRespectCache()
    {
        var fetchCount = 0;
        string? dependencyValue = null;

        var options = new QueryOptions<string>(
            queryKey: new QueryKey("dependent", dependencyValue ?? "none"),
            queryFn: async ctx => {
                fetchCount++;
                await Task.Delay(10);
                return $"Data for {dependencyValue}";
            },
            staleTime: TimeSpan.FromSeconds(10),
            enabled: dependencyValue != null
        );

        var query = new UseQuery<string>(options, _client);

        // Disabled initially
        await query.ExecuteAsync();
        Assert.Equal(0, fetchCount);

        // Enable with dependency
        dependencyValue = "value1";
        options.Enabled = true;
        
        // Create new query with same key but enabled
        var enabledQuery = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("dependent", dependencyValue),
                queryFn: async ctx => {
                    fetchCount++;
                    await Task.Delay(10);
                    return $"Data for {dependencyValue}";
                },
                staleTime: TimeSpan.FromSeconds(10),
                enabled: true
            ),
            _client
        );

        await enabledQuery.ExecuteAsync();
        Assert.Equal(1, fetchCount);
        Assert.Equal("Data for value1", enabledQuery.Data);
    }

    public void Dispose()
    {
        _client?.Dispose();
    }

    // Helper classes
    private class User
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    private class Project
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}

