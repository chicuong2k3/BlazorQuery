---
title: "Parallel Queries"
description: "Fetching multiple queries"
order: 6
category: "Guides"
---

# Parallel Queries

"Parallel" queries are queries that are executed in parallel, or at the same time, to maximize fetching concurrency.

## Manual Parallel Queries

When the number of parallel queries does not change, there is **no extra effort** to use parallel queries. Just use multiple `UseQuery` instances side-by-side!

```csharp
public class MyComponent
{
    private readonly QueryClient _queryClient;
    
    public async Task LoadDataAsync()
    {
        // The following queries will execute in parallel
        var usersQuery = new UseQuery<List<User>>(
            new QueryOptions<List<User>>(
                queryKey: new("users"),
                queryFn: async ctx => await FetchUsersAsync()
            ),
            _queryClient
        );
        
        var teamsQuery = new UseQuery<List<Team>>(
            new QueryOptions<List<Team>>(
                queryKey: new("teams"),
                queryFn: async ctx => await FetchTeamsAsync()
            ),
            _queryClient
        );
        
        var projectsQuery = new UseQuery<List<Project>>(
            new QueryOptions<List<Project>>(
                queryKey: new("projects"),
                queryFn: async ctx => await FetchProjectsAsync()
            ),
            _queryClient
        );
        
        // Execute all queries in parallel
        await Task.WhenAll(
            usersQuery.ExecuteAsync(),
            teamsQuery.ExecuteAsync(),
            projectsQuery.ExecuteAsync()
        );
        
        // Access data
        var users = usersQuery.Data;
        var teams = teamsQuery.Data;
        var projects = projectsQuery.Data;
    }
}
```

## Dynamic Parallel Queries with `UseQueries`

If the number of queries you need to execute is changing dynamically, you cannot manually create query instances. Instead, SwrSharp provides a `UseQueries<T>` class, which you can use to dynamically execute as many queries in parallel as you'd like.

### Basic Usage

`UseQueries<T>` accepts a list of `QueryOptions<T>` and manages multiple queries for you:

```csharp
public class MyComponent
{
    private readonly QueryClient _queryClient;
    private readonly UseQueries<User> _userQueries;
    
    public MyComponent(QueryClient queryClient)
    {
        _queryClient = queryClient;
        _userQueries = new UseQueries<User>(queryClient);
    }
    
    public async Task LoadUsersAsync(List<int> userIds)
    {
        // Create query options for each user ID
        var queryOptions = userIds.Select(id => 
            new QueryOptions<User>(
                queryKey: new("user", id),
                queryFn: async ctx => {
                    var userId = (int)ctx.QueryKey[1]!;
                    return await FetchUserByIdAsync(userId);
                }
            )
        );
        
        // Set the queries - this will create/recreate UseQuery instances
        _userQueries.SetQueries(queryOptions);
        
        // Execute all queries in parallel
        await _userQueries.ExecuteAllAsync();
        
        // Access individual query results
        foreach (var query in _userQueries.Queries)
        {
            if (query.IsSuccess)
            {
                var user = query.Data;
                Console.WriteLine($"Loaded user: {user.Name}");
            }
            else if (query.IsError)
            {
                Console.WriteLine($"Error: {query.Error.Message}");
            }
        }
    }
}
```

### Reacting to Changes

`UseQueries<T>` provides an `OnChange` event that fires whenever any of the managed queries change:

```csharp
public class MyComponent
{
    private readonly UseQueries<User> _userQueries;
    
    public MyComponent(QueryClient queryClient)
    {
        _userQueries = new UseQueries<User>(queryClient);
        
        // Subscribe to changes
        _userQueries.OnChange += HandleQueriesChanged;
    }
    
    private void HandleQueriesChanged()
    {
        // Check if all queries are done
        var allDone = _userQueries.Queries.All(q => 
            q.Status == QueryStatus.Success || q.Status == QueryStatus.Error
        );
        
        if (allDone)
        {
            Console.WriteLine("All queries completed!");
            
            // Get successful results
            var successfulUsers = _userQueries.Queries
                .Where(q => q.IsSuccess)
                .Select(q => q.Data)
                .ToList();
                
            Console.WriteLine($"Loaded {successfulUsers.Count} users");
        }
    }
}
```

### Advanced: Mixed Types with Non-Generic `UseQueries`

If you need to execute queries with different return types, use the non-generic `UseQueries` class:

```csharp
public class MyComponent
{
    private readonly QueryClient _queryClient;
    private readonly UseQueries _queries;
    
    public MyComponent(QueryClient queryClient)
    {
        _queryClient = queryClient;
        _queries = new UseQueries(queryClient);
    }
    
    public async Task LoadMixedDataAsync()
    {
        // Define queries with different types
        var queryDefinitions = new[]
        {
            (
                (object)new QueryOptions<List<User>>(
                    queryKey: new("users"),
                    queryFn: async ctx => await FetchUsersAsync()
                ),
                typeof(List<User>)
            ),
            (
                (object)new QueryOptions<List<Team>>(
                    queryKey: new("teams"),
                    queryFn: async ctx => await FetchTeamsAsync()
                ),
                typeof(List<Team>)
            )
        };
        
        _queries.SetQueries(queryDefinitions);
        await _queries.ExecuteAllAsync();
    }
}
```

**Note**: The non-generic version uses reflection and is less type-safe. Prefer `UseQueries<T>` when all queries return the same type.

## Query Options with UseQueries

All standard query options work with `UseQueries`:

```csharp
var queryOptions = userIds.Select(id => 
    new QueryOptions<User>(
        queryKey: new("user", id),
        queryFn: async ctx => await FetchUserByIdAsync(id),
        staleTime: TimeSpan.FromMinutes(5),    // Cache for 5 minutes
        retry: 3,                               // Retry 3 times on failure
        networkMode: NetworkMode.Online         // Only fetch when online
    )
);

_userQueries.SetQueries(queryOptions);
await _userQueries.ExecuteAllAsync();
```

## Refetching All Queries

You can refetch all queries at once:

```csharp
// Refetch all queries in parallel
await _userQueries.RefetchAllAsync();
```

## Example: Loading User Details

Complete example showing dynamic parallel queries:

```csharp
public class UserListComponent : IDisposable
{
    private readonly QueryClient _queryClient;
    private readonly UseQueries<UserDetails> _userDetailsQueries;
    
    public UserListComponent(QueryClient queryClient)
    {
        _queryClient = queryClient;
        _userDetailsQueries = new UseQueries<UserDetails>(queryClient);
        _userDetailsQueries.OnChange += UpdateUI;
    }
    
    public async Task LoadUserDetailsAsync(List<string> usernames)
    {
        // Create query for each username
        var queries = usernames.Select(username =>
            new QueryOptions<UserDetails>(
                queryKey: new("userDetails", username),
                queryFn: async ctx => {
                    var (queryKey, signal) = ctx;
                    var name = (string)queryKey[1]!;
                    return await _api.GetUserDetailsAsync(name, signal);
                },
                staleTime: TimeSpan.FromMinutes(10),
                retry: 2
            )
        );
        
        _userDetailsQueries.SetQueries(queries);
        await _userDetailsQueries.ExecuteAllAsync();
    }
    
    private void UpdateUI()
    {
        var queries = _userDetailsQueries.Queries;
        
        // Show loading state
        var isLoading = queries.Any(q => q.IsLoading);
        if (isLoading)
        {
            Console.WriteLine("Loading user details...");
            return;
        }
        
        // Show results
        foreach (var query in queries)
        {
            if (query.IsSuccess && query.Data != null)
            {
                Console.WriteLine($"User: {query.Data.Username} - {query.Data.Email}");
            }
            else if (query.IsError)
            {
                Console.WriteLine($"Error loading user: {query.Error?.Message}");
            }
        }
    }
    
    public void Dispose()
    {
        _userDetailsQueries.OnChange -= UpdateUI;
        _userDetailsQueries.Dispose();
    }
}
```

## Best Practices

### 1. **Dispose Resources**
Always dispose `UseQueries` when done:

```csharp
public void Dispose()
{
    _userQueries.Dispose();
}
```

### 2. **Use Reusable Query Factories**
Create factory methods for common query patterns:

```csharp
static QueryOptions<User> UserQueryOptions(int userId) =>
    new(
        queryKey: new("user", userId),
        queryFn: async ctx => await FetchUserAsync(userId),
        staleTime: TimeSpan.FromMinutes(5)
    );

// Usage
var queries = userIds.Select(id => UserQueryOptions(id));
_userQueries.SetQueries(queries);
```

### 3. **Handle Partial Failures**
Some queries may succeed while others fail:

```csharp
var results = _userQueries.Queries;
var successful = results.Where(q => q.IsSuccess).ToList();
var failed = results.Where(q => q.IsError).ToList();

Console.WriteLine($"Loaded {successful.Count}/{results.Count} users");
if (failed.Any())
{
    Console.WriteLine($"Failed: {string.Join(", ", failed.Select(q => q.Error?.Message))}");
}
```

### 4. **Optimize with StaleTime**
Prevent unnecessary refetches by using appropriate stale times:

```csharp
var queries = userIds.Select(id =>
    new QueryOptions<User>(
        queryKey: new("user", id),
        queryFn: async ctx => await FetchUserAsync(id),
        staleTime: TimeSpan.FromMinutes(10) // Cache for 10 minutes
    )
);
```

## Comparison with React Query

### React Query (TypeScript):
```typescript
const userQueries = useQueries({
  queries: users.map((user) => ({
    queryKey: ['user', user.id],
    queryFn: () => fetchUserById(user.id),
  })),
})
```

### SwrSharp (C#):
```csharp
var userQueries = new UseQueries<User>(queryClient);
userQueries.SetQueries(
    users.Select(user => new QueryOptions<User>(
        queryKey: new("user", user.Id),
        queryFn: async ctx => await FetchUserByIdAsync(user.Id)
    ))
);
await userQueries.ExecuteAllAsync();
```

**Key Differences**:
- SwrSharp requires explicit `ExecuteAllAsync()` call
- SwrSharp uses class instances instead of hooks
- Type parameter `<T>` is required in C#
