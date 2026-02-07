---
title: "Query Cancellation"
description: "Guide for Query Cancellation in SwrSharp"
order: 17
category: "Guides"
---
# Query Cancellation

SwrSharp provides each query function with a `CancellationToken` through the `QueryFunctionContext`. When a query becomes out-of-date or inactive, this token can be cancelled. This means that all queries are cancellable, and you can respond to the cancellation inside your query function if desired. The best part about this is that it allows you to continue to use normal async/await syntax while getting all the benefits of automatic cancellation.

The `CancellationToken` API is a standard part of .NET and is available in all modern .NET versions.

## Default Behavior

By default, queries that are disposed or become unused before their promises are resolved are _not_ automatically cancelled. This means that after the task completes, the resulting data will be available in the cache. This is helpful if you've started receiving a query, but then dispose the component before it finishes. If you create the component again and the query has not been garbage collected yet, data will be available.

However, if you consume the `CancellationToken` (`ctx.Signal`), the Task will be cancelled and therefore, also the Query must be cancelled. Cancelling the query will result in its state being _reverted_ to its previous state (if `Revert = true` in cancel options).

## Using HttpClient with CancellationToken

```csharp
var query = new UseQuery<List<Todo>>(
    new QueryOptions<List<Todo>>(
        queryKey: new("todos"),
        queryFn: async ctx => {
            // Pass the cancellation token to HttpClient
            var response = await httpClient.GetAsync("/api/todos", ctx.Signal);
            return await response.Content.ReadFromJsonAsync<List<Todo>>(
                cancellationToken: ctx.Signal
            ) ?? new List<Todo>();
        }
    ),
    queryClient
);
```

## Using Multiple Requests with CancellationToken

```csharp
var query = new UseQuery<List<TodoDetail>>(
    new QueryOptions<List<TodoDetail>>(
        queryKey: new("todos"),
        queryFn: async ctx => {
            // First request
            var todosResponse = await httpClient.GetAsync(
                "/api/todos", 
                ctx.Signal // Pass token to first fetch
            );
            var todos = await todosResponse.Content
                .ReadFromJsonAsync<List<Todo>>(cancellationToken: ctx.Signal);

            // Multiple parallel requests
            var todoDetails = todos!.Select(async todo => {
                var response = await httpClient.GetAsync(
                    $"/api/todos/{todo.Id}/details",
                    ctx.Signal // Pass token to each fetch
                );
                return await response.Content
                    .ReadFromJsonAsync<TodoDetail>(cancellationToken: ctx.Signal);
            });

            return (await Task.WhenAll(todoDetails))
                .Where(d => d != null)
                .Cast<TodoDetail>()
                .ToList();
        }
    ),
    queryClient
);
```

## Using Task.Delay with CancellationToken

```csharp
var query = new UseQuery<string>(
    new QueryOptions<string>(
        queryKey: new("data"),
        queryFn: async ctx => {
            // Simulate delay that respects cancellation
            await Task.Delay(5000, ctx.Signal);
            return "Data after delay";
        }
    ),
    queryClient
);
```

## Using Custom API Clients

### Example: Refit

```csharp
public interface ITodoApi
{
    [Get("/todos")]
    Task<List<Todo>> GetTodosAsync(CancellationToken cancellationToken);
}

var query = new UseQuery<List<Todo>>(
    new QueryOptions<List<Todo>>(
        queryKey: new("todos"),
        queryFn: async ctx => {
            // Refit automatically uses the cancellation token
            return await todoApi.GetTodosAsync(ctx.Signal);
        }
    ),
    queryClient
);
```

### Example: Custom HttpClient Wrapper

```csharp
public class ApiClient
{
    private readonly HttpClient _http;

    public async Task<T> GetAsync<T>(string url, CancellationToken ct)
    {
        var response = await _http.GetAsync(url, ct);
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
    }
}

var query = new UseQuery<List<Todo>>(
    new QueryOptions<List<Todo>>(
        queryKey: new("todos"),
        queryFn: async ctx => {
            return await apiClient.GetAsync<List<Todo>>("/api/todos", ctx.Signal);
        }
    ),
    queryClient
);
```

## Manual Cancellation

You might want to cancel a query manually. For example, if the request takes a long time to finish, you can allow the user to click a cancel button to stop the request. To do this, you just need to call `queryClient.CancelQueries(filters)`, which will cancel the query and revert it back to its previous state. If you have consumed the `Signal` (CancellationToken) in the query function, SwrSharp will additionally also cancel the Task.

```csharp
var query = new UseQuery<List<Todo>>(
    new QueryOptions<List<Todo>>(
        queryKey: new("todos"),
        queryFn: async ctx => {
            var response = await httpClient.GetAsync("/api/todos", ctx.Signal);
            return await response.Content.ReadFromJsonAsync<List<Todo>>(
                cancellationToken: ctx.Signal
            ) ?? new List<Todo>();
        }
    ),
    queryClient
);

// In your UI, add a cancel button handler
private void OnCancelButtonClick()
{
    queryClient.CancelQueries(new QueryFilters
    {
        QueryKey = new("todos")
    });
}
```

## Complete Example: Long-Running Query with Cancel Button

```csharp
public class TodosComponent : IDisposable
{
    private readonly QueryClient _queryClient;
    private readonly HttpClient _httpClient;
    private UseQuery<List<Todo>>? _todosQuery;

    public TodosComponent(HttpClient httpClient, QueryClient queryClient)
    {
        _httpClient = httpClient;
        _queryClient = queryClient;
    }

    public async Task LoadTodosAsync()
    {
        _todosQuery = new UseQuery<List<Todo>>(
            new QueryOptions<List<Todo>>(
                queryKey: new("todos"),
                queryFn: async ctx => {
                    Console.WriteLine("Starting long fetch...");
                    
                    // Simulate long-running request
                    await Task.Delay(10000, ctx.Signal);
                    
                    var response = await _httpClient.GetAsync(
                        "/api/todos", 
                        ctx.Signal
                    );
                    
                    return await response.Content.ReadFromJsonAsync<List<Todo>>(
                        cancellationToken: ctx.Signal
                    ) ?? new List<Todo>();
                }
            ),
            _queryClient
        );

        _todosQuery.OnChange += RenderUI;

        await _todosQuery.ExecuteAsync();
    }

    public void OnCancelClick()
    {
        Console.WriteLine("User clicked cancel");
        
        _queryClient.CancelQueries(new QueryFilters
        {
            QueryKey = new("todos")
        });
    }

    private void RenderUI()
    {
        if (_todosQuery == null) return;

        Console.WriteLine("=== Todos ===");
        
        if (_todosQuery.IsFetching)
        {
            Console.WriteLine("Fetching... [Cancel]");
        }
        else if (_todosQuery.IsLoading)
        {
            Console.WriteLine("Loading...");
        }
        else if (_todosQuery.IsError)
        {
            Console.WriteLine($"Error: {_todosQuery.Error?.Message}");
        }
        else if (_todosQuery.Data != null)
        {
            foreach (var todo in _todosQuery.Data)
            {
                Console.WriteLine($"  - {todo.Title}");
            }
        }
    }

    public void Dispose()
    {
        _todosQuery?.Dispose();
    }
}
```

## Cancel Options

Cancel options are used to control the behavior of query cancellation operations.

```csharp
// Cancel specific queries silently
queryClient.CancelQueries(
    new QueryFilters { QueryKey = new("posts") }, 
    new CancelOptions { Silent = true }
);

// Cancel without reverting state
queryClient.CancelQueries(
    new QueryFilters { QueryKey = new("todos") },
    new CancelOptions { Revert = false }
);
```

A `CancelOptions` object supports the following properties:

### `Silent`
```csharp
public bool Silent { get; set; } = false
```
- When set to `true`, suppresses propagation of `OperationCanceledException` to observers (e.g., `OnError` callbacks) and related notifications.
- Defaults to `false`

### `Revert`
```csharp
public bool Revert { get; set; } = true
```
- When set to `true`, restores the query's state (data and status) from immediately before the in-flight fetch, sets `FetchStatus` back to `Idle`, and only throws if there was no prior data.
- Defaults to `true`

## Advanced Examples

### Example 1: Cancel All Queries

```csharp
public class GlobalActions
{
    private readonly QueryClient _queryClient;

    public void CancelAllQueries()
    {
        // Cancel everything
        _queryClient.CancelQueries();
        Console.WriteLine("All queries cancelled");
    }
}
```

### Example 2: Cancel by Prefix

```csharp
public class TodoActions
{
    private readonly QueryClient _queryClient;

    public void CancelAllTodoQueries()
    {
        // Cancel all queries starting with "todos"
        _queryClient.CancelQueries(new QueryFilters
        {
            QueryKey = new("todos")
        });
    }
}
```

### Example 3: Cancel with Predicate

```csharp
public class AdvancedCancellation
{
    private readonly QueryClient _queryClient;

    public void CancelOldTodoQueries()
    {
        // Cancel todos with id > 100
        _queryClient.CancelQueries(new QueryFilters
        {
            Predicate = key => {
                if (key.Parts.Count < 2) return false;
                if (key.Parts[0]?.ToString() != "todos") return false;
                
                var id = key.Parts[1] as int?;
                return id.HasValue && id.Value > 100;
            }
        });
    }
}
```

### Example 4: Timeout Pattern

```csharp
public class TimeoutQuery
{
    public async Task<List<Todo>> LoadWithTimeoutAsync()
    {
        var query = new UseQuery<List<Todo>>(
            new QueryOptions<List<Todo>>(
                queryKey: new("todos"),
                queryFn: async ctx => {
                    // Use CancellationTokenSource.CreateLinkedTokenSource 
                    // to combine timeout with query cancellation
                    using var timeoutCts = new CancellationTokenSource(
                        TimeSpan.FromSeconds(5)
                    );
                    using var linkedCts = CancellationTokenSource
                        .CreateLinkedTokenSource(ctx.Signal, timeoutCts.Token);

                    var response = await httpClient.GetAsync(
                        "/api/todos", 
                        linkedCts.Token
                    );
                    
                    return await response.Content
                        .ReadFromJsonAsync<List<Todo>>(
                            cancellationToken: linkedCts.Token
                        ) ?? new List<Todo>();
                }
            ),
            queryClient
        );

        await query.ExecuteAsync();
        return query.Data ?? new List<Todo>();
    }
}
```

## Best Practices

### 1. **Always Pass CancellationToken to Async Operations**

```csharp
// ✅ Good: Pass token to all async operations
queryFn: async ctx => {
    var response = await httpClient.GetAsync("/api/data", ctx.Signal);
    return await response.Content.ReadFromJsonAsync<Data>(
        cancellationToken: ctx.Signal
    );
}

// ❌ Bad: Ignore cancellation token
queryFn: async ctx => {
    var response = await httpClient.GetAsync("/api/data");
    return await response.Content.ReadFromJsonAsync<Data>();
}
```

### 2. **Handle OperationCanceledException Gracefully**

```csharp
// ✅ Good: Let SwrSharp handle cancellation
queryFn: async ctx => {
    var response = await httpClient.GetAsync("/api/data", ctx.Signal);
    return await response.Content.ReadFromJsonAsync<Data>(
        cancellationToken: ctx.Signal
    );
}

// ❌ Bad: Swallow cancellation exception
queryFn: async ctx => {
    try {
        var response = await httpClient.GetAsync("/api/data", ctx.Signal);
        return await response.Content.ReadFromJsonAsync<Data>();
    } catch (OperationCanceledException) {
        return null; // Don't do this!
    }
}
```

### 3. **Use CancelQueries for User-Initiated Cancellation**

```csharp
// ✅ Good: Provide cancel button
private void OnCancelClick()
{
    queryClient.CancelQueries(new QueryFilters 
    { 
        QueryKey = new("longRunningQuery") 
    });
}

// ❌ Bad: No way to cancel
// Users stuck waiting for long request
```

### 4. **Consider Silent Mode for Background Queries**

```csharp
// ✅ Good: Silent cancellation for background work
queryClient.CancelQueries(
    new QueryFilters { QueryKey = new("backgroundSync") },
    new CancelOptions { Silent = true }
);

// Normal cancellation for user-facing queries
queryClient.CancelQueries(
    new QueryFilters { QueryKey = new("userAction") }
);
```

## Comparison with React Query

### React Query (TypeScript):
```typescript
// Using fetch with AbortSignal
const query = useQuery({
  queryKey: ['todos'],
  queryFn: async ({ signal }) => {
    const response = await fetch('/todos', { signal })
    return response.json()
  }
})

// Manual cancellation
queryClient.cancelQueries({ queryKey: ['todos'] })

// With options
await queryClient.cancelQueries(
  { queryKey: ['posts'] },
  { silent: true }
)
```

### SwrSharp (C#):
```csharp
// Using HttpClient with CancellationToken
var query = new UseQuery<List<Todo>>(
    new QueryOptions<List<Todo>>(
        queryKey: new("todos"),
        queryFn: async ctx => {
            var response = await httpClient.GetAsync("/todos", ctx.Signal);
            return await response.Content.ReadFromJsonAsync<List<Todo>>(
                cancellationToken: ctx.Signal
            ) ?? new List<Todo>();
        }
    ),
    queryClient
);

// Manual cancellation
queryClient.CancelQueries(new QueryFilters { QueryKey = new("todos") });

// With options
queryClient.CancelQueries(
    new QueryFilters { QueryKey = new("posts") },
    new CancelOptions { Silent = true }
);
```

---

## Summary

- ✅ Every query function receives `CancellationToken` via `ctx.Signal`
- ✅ Pass token to all async operations (HttpClient, Task.Delay, etc.)
- ✅ Manual cancellation via `CancelQueries()`
- ✅ Cancel with filters (prefix, exact, predicate)
- ✅ `CancelOptions` for silent/revert control
- ✅ Default behavior: cancellation reverts state
- ✅ Perfect for: long-running requests, user cancellation, timeouts
- ✅ Standard .NET `CancellationToken` - works with all APIs

**Use cancellation for better UX and resource management!** 🎯

