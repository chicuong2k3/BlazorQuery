---
title: "Important Defaults"
description: "Default configuration"
order: 18
category: "Guides"
---

# Important Defaults

Out of the box, SwrSharp is configured with **aggressive but sane** defaults. **Sometimes these defaults can catch new users off guard or make learning/debugging difficult if they are unknown by the user.** Keep them in mind as you continue to learn and use SwrSharp:

## Stale Data by Default

- Query instances via `UseQuery` or `UseInfiniteQuery` by default **consider cached data as stale**.

```csharp
// Default behavior: staleTime = TimeSpan.Zero
var query = new UseQuery<List<Todo>>(
    new QueryOptions<List<Todo>>(
        queryKey: new("todos"),
        queryFn: async ctx => await FetchTodosAsync()
        // No staleTime specified = TimeSpan.Zero (always stale)
    ),
    queryClient
);
```

> To change this behavior, you can configure your queries both globally and per-query using the `staleTime` option. Specifying a longer `staleTime` means queries will not refetch their data as often.

## StaleTime Configuration

- A Query that has a `staleTime` set is considered **fresh** until that `staleTime` has elapsed.

```csharp
// Set staleTime to 2 minutes - data stays fresh for 2 minutes
var query = new UseQuery<List<Todo>>(
    new QueryOptions<List<Todo>>(
        queryKey: new("todos"),
        queryFn: async ctx => await FetchTodosAsync(),
        staleTime: TimeSpan.FromMinutes(2) // Fresh for 2 minutes
    ),
    queryClient
);

// Set staleTime to never expire (until manually invalidated)
var query = new UseQuery<List<Todo>>(
    new QueryOptions<List<Todo>>(
        queryKey: new("todos"),
        queryFn: async ctx => await FetchTodosAsync(),
        staleTime: TimeSpan.MaxValue // Effectively "Infinity"
    ),
    queryClient
);
```

**Note**: C# doesn't have a "static" equivalent like React Query. Use `TimeSpan.MaxValue` for very long cache times, and manually invalidate when needed.

## Automatic Background Refetching

Stale queries are refetched automatically in the background when:

1. **New instances of the query mount**
2. **The window is refocused** (if `refetchOnWindowFocus` is enabled)
3. **The network is reconnected** (if `refetchOnReconnect` is enabled)

```csharp
var query = new UseQuery<List<Todo>>(
    new QueryOptions<List<Todo>>(
        queryKey: new("todos"),
        queryFn: async ctx => await FetchTodosAsync(),
        staleTime: TimeSpan.FromMinutes(5),
        refetchOnWindowFocus: true,  // Default: true
        refetchOnReconnect: true     // Default: true
    ),
    queryClient
);
```

> Setting `staleTime` is the recommended way to avoid excessive refetches, but you can also customize the refetch behavior by setting options like `refetchOnWindowFocus` and `refetchOnReconnect`.

## Polling with RefetchInterval

Queries can optionally be configured with a `refetchInterval` to trigger refetches periodically, which is independent of the `staleTime` setting:

```csharp
var query = new UseQuery<List<Todo>>(
    new QueryOptions<List<Todo>>(
        queryKey: new("todos"),
        queryFn: async ctx => await FetchTodosAsync(),
        refetchInterval: TimeSpan.FromSeconds(30) // Poll every 30 seconds
    ),
    queryClient
);

// Polling happens regardless of staleTime
// Even if data is fresh, it will still refetch every 30 seconds
```

## Garbage Collection

- In React Query, query results that have no more active observers are labeled as "inactive" and remain in the cache for **5 minutes** (default `gcTime`) before being garbage collected.
- **SwrSharp does not yet implement automatic garbage collection**. Cache entries persist until manually removed.

```csharp
// SwrSharp: Cache entries persist until manually removed
var query = new UseQuery<List<Todo>>(...);
await query.ExecuteAsync();
query.Dispose(); // Query instance disposed, but cache entry remains in QueryClient

// To manually remove cached data:
queryClient.Invalidate(new QueryKey("todos"));

// Cache is also cleared when QueryClient is disposed:
queryClient.Dispose();
```

> **Note**: Automatic time-based GC with configurable `gcTime` is planned for future releases. For now, you must explicitly manage cache cleanup via `QueryClient.Invalidate()` or `QueryClient.Dispose()`.

## Retry Behavior

Queries that fail are **silently retried 3 times, with exponential backoff delay** before capturing and displaying an error to the UI:

```csharp
// Default retry behavior:
// - retry: 3 (3 retries after initial attempt = 4 total attempts)
// - retryDelay: exponential backoff (1s, 2s, 4s, capped at 30s)

var query = new UseQuery<List<Todo>>(
    new QueryOptions<List<Todo>>(
        queryKey: new("todos"),
        queryFn: async ctx => await FetchTodosAsync()
        // Default: retry = 3
        // Default: exponential backoff delay
    ),
    queryClient
);

// Customize retry behavior
var customQuery = new UseQuery<List<Todo>>(
    new QueryOptions<List<Todo>>(
        queryKey: new("todos"),
        queryFn: async ctx => await FetchTodosAsync(),
        retry: 5, // Retry 5 times
        retryDelayFunc: attemptIndex => TimeSpan.FromSeconds(2) // Fixed 2s delay
    ),
    queryClient
);
```

> To change this, you can alter the default `retry` and `retryDelayFunc` options for queries.

## Structural Sharing

Query results by default use **reference equality checks to detect if data has actually changed** and if not, **the data reference remains unchanged** to better help with value stabilization.

```csharp
// SwrSharp uses reference equality for objects
public T? Data 
{ 
    get => _data;
    private set
    {
        // Only notify if reference actually changed
        if (Equals(_data, value))
            return;
        _data = value;
        Notify(); // Fires OnChange
    }
}

// Example:
var query = new UseQuery<List<Todo>>(...);
await query.ExecuteAsync(); // Fetches data

var oldData = query.Data;

await query.RefetchAsync(); // Refetch

// If API returns same data (new instance but equal content):
// - C# will still have new reference (new List<Todo>)
// - OnChange will fire because reference changed
// - This is different from React Query's deep structural sharing
```

> **Note**: Unlike React Query's deep structural sharing, SwrSharp uses reference equality. For best performance with immutable data, consider using immutable collections or records where the same data produces the same reference.

## Summary of Defaults

| Setting | Default Value | Description |
|---------|---------------|-------------|
| `staleTime` | `TimeSpan.Zero` | Data is always considered stale |
| `refetchOnWindowFocus` | `true` | Refetch when window gains focus |
| `refetchOnReconnect` | `true` | Refetch when network reconnects |
| `refetchInterval` | `null` | No automatic polling |
| `gcTime` | N/A* | Not yet implemented (manual cleanup) |
| `retry` | `3` | Retry 3 times after initial attempt |
| `retryDelay` | Exponential | `Math.Min(1000 * 2^attempt, 30000)` |
| `enabled` | `true` | Query executes automatically |
| `networkMode` | `Online` | Pause when offline |

*Automatic GC with configurable `gcTime` planned for future release

## Common Pitfalls

### 1. **Data Always Refetching**

```csharp
// ❌ Problem: staleTime = TimeSpan.Zero (default)
var query = new UseQuery<List<Todo>>(
    new QueryOptions<List<Todo>>(
        queryKey: new("todos"),
        queryFn: async ctx => await FetchTodosAsync()
    ),
    queryClient
);
// Every mount triggers a refetch!

// ✅ Solution: Set appropriate staleTime
var query = new UseQuery<List<Todo>>(
    new QueryOptions<List<Todo>>(
        queryKey: new("todos"),
        queryFn: async ctx => await FetchTodosAsync(),
        staleTime: TimeSpan.FromMinutes(5) // Fresh for 5 minutes
    ),
    queryClient
);
```

### 2. **Forgetting to Dispose**

```csharp
// ❌ Problem: Query not disposed
public class TodosComponent : ComponentBase
{
    private UseQuery<List<Todo>>? _query;

    protected override async Task OnInitializedAsync()
    {
        _query = new UseQuery<List<Todo>>(...);
        await _query.ExecuteAsync();
    }
    // Missing Dispose() - memory leak!
}

// ✅ Solution: Always dispose
public class TodosComponent : ComponentBase, IDisposable
{
    private UseQuery<List<Todo>>? _query;

    protected override async Task OnInitializedAsync()
    {
        _query = new UseQuery<List<Todo>>(...);
        _query.OnChange += StateHasChanged;
        await _query.ExecuteAsync();
    }

    public void Dispose()
    {
        if (_query != null)
        {
            _query.OnChange -= StateHasChanged;
            _query.Dispose();
        }
    }
}
```

### 3. **Unexpected Retries**

```csharp
// ❌ Problem: Queries retry 3 times by default
var query = new UseQuery<List<Todo>>(
    new QueryOptions<List<Todo>>(
        queryKey: new("todos"),
        queryFn: async ctx => {
            // This will be called up to 4 times total!
            return await FetchTodosAsync();
        }
    ),
    queryClient
);

// ✅ Solution: Disable retry if not needed
var query = new UseQuery<List<Todo>>(
    new QueryOptions<List<Todo>>(
        queryKey: new("todos"),
        queryFn: async ctx => await FetchTodosAsync(),
        retry: 0 // No retries
    ),
    queryClient
);
```

### 4. **Window Focus Refetching**

```csharp
// ❌ Problem: Unexpected refetch when returning to tab
// Default: refetchOnWindowFocus = true
// User switches tabs and comes back → automatic refetch

// ✅ Solution: Disable if not needed
var query = new UseQuery<List<Todo>>(
    new QueryOptions<List<Todo>>(
        queryKey: new("todos"),
        queryFn: async ctx => await FetchTodosAsync(),
        refetchOnWindowFocus: false // Disable focus refetch
    ),
    queryClient
);
```

### 5. **Network Mode and Offline Behavior**

```csharp
// ❌ Problem: Query pauses when offline
// Default: networkMode = Online
var query = new UseQuery<List<Todo>>(
    new QueryOptions<List<Todo>>(
        queryKey: new("todos"),
        queryFn: async ctx => await FetchTodosAsync()
        // Will pause if offline!
    ),
    queryClient
);

// ✅ Solution: Use appropriate network mode
var query = new UseQuery<List<Todo>>(
    new QueryOptions<List<Todo>>(
        queryKey: new("todos"),
        queryFn: async ctx => await FetchTodosAsync(),
        networkMode: NetworkMode.Always // Always fetch regardless
    ),
    queryClient
);
```

## Complete Example: Production-Ready Query

```csharp
public class ProductionQuery : ComponentBase, IDisposable
{
    [Inject] private QueryClient QueryClient { get; set; } = null!;
    [Inject] private HttpClient Http { get; set; } = null!;

    private UseQuery<List<Todo>>? _todosQuery;

    protected override async Task OnInitializedAsync()
    {
        _todosQuery = new UseQuery<List<Todo>>(
            new QueryOptions<List<Todo>>(
                queryKey: new("todos"),
                queryFn: async ctx => {
                    var response = await Http.GetAsync("/api/todos", ctx.Signal);
                    response.EnsureSuccessStatusCode();
                    return await response.Content
                        .ReadFromJsonAsync<List<Todo>>(cancellationToken: ctx.Signal)
                        ?? new List<Todo>();
                },
                
                // Cache for 5 minutes
                staleTime: TimeSpan.FromMinutes(5),
                
                // Refetch on window focus (good for real-time data)
                refetchOnWindowFocus: true,
                
                // Refetch when network reconnects
                refetchOnReconnect: true,
                
                // Retry 3 times with exponential backoff
                retry: 3,
                
                // Poll every 30 seconds (optional)
                // refetchInterval: TimeSpan.FromSeconds(30),
                
                // Online mode (pause when offline)
                networkMode: NetworkMode.Online
            ),
            QueryClient
        );

        _todosQuery.OnChange += StateHasChanged;
        await _todosQuery.ExecuteAsync();
    }

    public void Dispose()
    {
        if (_todosQuery != null)
        {
            _todosQuery.OnChange -= StateHasChanged;
            _todosQuery.Dispose();
        }
    }
}
```

## Recommended Defaults for Different Scenarios

### Real-Time Data (Stock Prices, Chat)
```csharp
staleTime: TimeSpan.Zero,              // Always stale
refetchInterval: TimeSpan.FromSeconds(5), // Poll every 5s
refetchOnWindowFocus: true,            // Refetch on focus
retry: 1                               // Quick failure
```

### Static Content (Blog Posts, Documentation)
```csharp
staleTime: TimeSpan.FromHours(1),      // Cache for 1 hour
refetchOnWindowFocus: false,           // No refetch on focus
refetchOnReconnect: false,             // No refetch on reconnect
retry: 3                               // Standard retry
```

### User-Specific Data (Profile, Settings)
```csharp
staleTime: TimeSpan.FromMinutes(5),    // Cache for 5 minutes
refetchOnWindowFocus: true,            // Refetch on focus
refetchOnReconnect: true,              // Refetch on reconnect
retry: 3                               // Standard retry
```

### Expensive Queries (Analytics, Reports)
```csharp
staleTime: TimeSpan.FromMinutes(30),   // Cache for 30 minutes
refetchOnWindowFocus: false,           // No refetch on focus
refetchOnReconnect: false,             // No refetch on reconnect
retry: 5                               // More retries for reliability
```
