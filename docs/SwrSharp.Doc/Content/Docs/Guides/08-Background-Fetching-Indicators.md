---
title: "Background Fetching"
description: "Background refetch indicators"
order: 8
category: "Guides"
---

# Background Fetching Indicators

A query's `Status == QueryStatus.Pending` state is sufficient to show the initial hard-loading state for a query, but sometimes you may want to display an additional indicator that a query is refetching in the background. To do this, queries provide you with an `IsFetching` boolean that you can use to show that it's in a fetching state, regardless of the state of the `Status` variable.

## Individual Query Fetching Indicator

Use the `IsFetching` property to show background refetch indicators:

```csharp
public class TodosComponent
{
    private readonly QueryClient _queryClient;
    private UseQuery<List<Todo>>? _todosQuery;

    public async Task LoadTodosAsync()
    {
        _todosQuery = new UseQuery<List<Todo>>(
            new QueryOptions<List<Todo>>(
                queryKey: new("todos"),
                queryFn: async ctx => await FetchTodosAsync()
            ),
            _queryClient
        );

        await _todosQuery.ExecuteAsync();
        RenderUI();
    }

    private void RenderUI()
    {
        if (_todosQuery == null) return;

        if (_todosQuery.Status == QueryStatus.Pending)
        {
            // Initial loading
            Console.WriteLine("Loading...");
        }
        else if (_todosQuery.Status == QueryStatus.Error)
        {
            // Error state
            Console.WriteLine($"Error: {_todosQuery.Error?.Message}");
        }
        else
        {
            // Success - show data
            if (_todosQuery.IsFetching)
            {
                // Background refetch indicator
                Console.WriteLine("Refreshing...");
            }

            foreach (var todo in _todosQuery.Data!)
            {
                Console.WriteLine($"- {todo.Title}");
            }
        }
    }
}
```

## IsFetching vs IsFetchingBackground

SwrSharp provides two related properties:

### `IsFetching`
- `true` when query is actively fetching (any fetch)
- Includes initial loads and background refetches
- Equivalent to `FetchStatus == FetchStatus.Fetching`

```csharp
var query = new UseQuery<Data>(options, queryClient);

if (query.IsFetching)
{
    // Query is currently fetching (initial or background)
}
```

### `IsFetchingBackground`
- `true` only when refetching with existing data
- `false` during initial load (no data yet)
- Useful to distinguish background refetch from initial load

```csharp
if (query.IsFetchingBackground)
{
    // Has old data, fetching new data
    Console.WriteLine("Updating...");
    Console.WriteLine($"Current data: {query.Data}"); // Old data still available
}
```

## Example: Loading States

```csharp
public class DataComponent
{
    private readonly QueryClient _queryClient;
    private UseQuery<List<Item>>? _query;

    public async Task LoadDataAsync()
    {
        _query = new UseQuery<List<Item>>(
            new QueryOptions<List<Item>>(
                queryKey: new("items"),
                queryFn: async ctx => await FetchItemsAsync(),
                staleTime: TimeSpan.FromSeconds(30)
            ),
            _queryClient
        );

        // Subscribe to changes for reactive UI
        _query.OnChange += RenderUI;

        await _query.ExecuteAsync();
    }

    private void RenderUI()
    {
        if (_query == null) return;

        // Initial loading (no data yet)
        if (_query.IsLoading)
        {
            Console.WriteLine("⏳ Loading data for the first time...");
            return;
        }

        // Error state
        if (_query.IsError)
        {
            Console.WriteLine($"❌ Error: {_query.Error?.Message}");
            return;
        }

        // Success with data
        if (_query.IsSuccess && _query.Data != null)
        {
            // Background refetch indicator
            if (_query.IsFetchingBackground)
            {
                Console.WriteLine("🔄 Refreshing data in background...");
            }

            Console.WriteLine($"✅ Loaded {_query.Data.Count} items");
            
            foreach (var item in _query.Data)
            {
                Console.WriteLine($"  - {item.Name}");
            }
        }
    }
}
```

## Global Background Fetching Indicator

In addition to individual query loading states, if you would like to show a global loading indicator when **any** queries are fetching (including in the background), you can use the `QueryClient.IsFetching` property:

```csharp
public class GlobalLoadingIndicator
{
    private readonly QueryClient _queryClient;

    public GlobalLoadingIndicator(QueryClient queryClient)
    {
        _queryClient = queryClient;
        
        // Subscribe to global fetching changes
        _queryClient.OnFetchingChanged += UpdateIndicator;
    }

    private void UpdateIndicator()
    {
        if (_queryClient.IsFetching)
        {
            Console.WriteLine("⏳ Queries are fetching in the background...");
            // Show global spinner or progress bar
        }
        else
        {
            Console.WriteLine("✅ All queries complete");
            // Hide global spinner
        }
    }
}
```

## Global Fetching with Event Handling

The `QueryClient` fires an event when global fetching state changes:

```csharp
public class App
{
    private readonly QueryClient _queryClient;
    private bool _showGlobalSpinner;

    public App()
    {
        _queryClient = new QueryClient();
        
        // React to global fetching state
        _queryClient.OnFetchingChanged += () => {
            _showGlobalSpinner = _queryClient.IsFetching;
            UpdateUI();
        };
    }

    private void UpdateUI()
    {
        if (_showGlobalSpinner)
        {
            // Show global loading indicator
            Console.WriteLine("╔════════════════════╗");
            Console.WriteLine("║  Loading...        ║");
            Console.WriteLine("╚════════════════════╝");
        }
    }

    public async Task RunQueriesAsync()
    {
        var query1 = new UseQuery<Data1>(
            new QueryOptions<Data1>(
                queryKey: new("data1"),
                queryFn: async ctx => await FetchData1Async()
            ),
            _queryClient
        );

        var query2 = new UseQuery<Data2>(
            new QueryOptions<Data2>(
                queryKey: new("data2"),
                queryFn: async ctx => await FetchData2Async()
            ),
            _queryClient
        );

        // Both will trigger global fetching indicator
        await Task.WhenAll(
            query1.ExecuteAsync(),
            query2.ExecuteAsync()
        );
    }
}
```

## Complete Example: Dashboard with Global Indicator

```csharp
public class Dashboard : IDisposable
{
    private readonly QueryClient _queryClient;
    private readonly List<IDisposable> _queries = new();
    private bool _showGlobalLoader;

    public Dashboard()
    {
        _queryClient = new QueryClient();
        _queryClient.OnFetchingChanged += UpdateGlobalLoader;
    }

    public async Task InitializeAsync()
    {
        // Create multiple queries
        var usersQuery = new UseQuery<List<User>>(
            new QueryOptions<List<User>>(
                queryKey: new("users"),
                queryFn: async ctx => await FetchUsersAsync(),
                staleTime: TimeSpan.FromMinutes(5)
            ),
            _queryClient
        );

        var statsQuery = new UseQuery<Stats>(
            new QueryOptions<Stats>(
                queryKey: new("stats"),
                queryFn: async ctx => await FetchStatsAsync(),
                staleTime: TimeSpan.FromMinutes(1)
            ),
            _queryClient
        );

        var alertsQuery = new UseQuery<List<Alert>>(
            new QueryOptions<List<Alert>>(
                queryKey: new("alerts"),
                queryFn: async ctx => await FetchAlertsAsync(),
                refetchInterval: TimeSpan.FromSeconds(30) // Poll every 30s
            ),
            _queryClient
        );

        _queries.AddRange(new[] { usersQuery, statsQuery, alertsQuery });

        // Subscribe to individual query changes
        usersQuery.OnChange += () => RenderUsers(usersQuery);
        statsQuery.OnChange += () => RenderStats(statsQuery);
        alertsQuery.OnChange += () => RenderAlerts(alertsQuery);

        // Execute all queries in parallel
        await Task.WhenAll(
            usersQuery.ExecuteAsync(),
            statsQuery.ExecuteAsync(),
            alertsQuery.ExecuteAsync()
        );
    }

    private void UpdateGlobalLoader()
    {
        _showGlobalLoader = _queryClient.IsFetching;
        
        if (_showGlobalLoader)
        {
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("  Syncing data...");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━");
        }
        else
        {
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("  All data up to date");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━");
        }
    }

    private void RenderUsers(UseQuery<List<User>> query)
    {
        Console.WriteLine("\n📊 Users:");
        if (query.IsLoading)
        {
            Console.WriteLine("  Loading users...");
        }
        else if (query.IsFetchingBackground)
        {
            Console.WriteLine($"  {query.Data!.Count} users (refreshing...)");
        }
        else if (query.IsSuccess)
        {
            Console.WriteLine($"  {query.Data!.Count} users");
        }
    }

    private void RenderStats(UseQuery<Stats> query)
    {
        Console.WriteLine("\n📈 Statistics:");
        if (query.IsLoading)
        {
            Console.WriteLine("  Loading stats...");
        }
        else if (query.IsFetchingBackground)
        {
            Console.WriteLine($"  Views: {query.Data!.Views} (updating...)");
        }
        else if (query.IsSuccess)
        {
            Console.WriteLine($"  Views: {query.Data!.Views}");
        }
    }

    private void RenderAlerts(UseQuery<List<Alert>> query)
    {
        Console.WriteLine("\n🔔 Alerts:");
        if (query.IsLoading)
        {
            Console.WriteLine("  Loading alerts...");
        }
        else if (query.IsFetchingBackground)
        {
            Console.WriteLine($"  {query.Data!.Count} alerts (checking for new...)");
        }
        else if (query.IsSuccess)
        {
            Console.WriteLine($"  {query.Data!.Count} alerts");
        }
    }

    public void Dispose()
    {
        _queryClient.OnFetchingChanged -= UpdateGlobalLoader;
        foreach (var query in _queries)
        {
            query.Dispose();
        }
        _queryClient.Dispose();
    }
}
```

## Use Cases

### Individual `IsFetching`:
- Show "Refreshing..." badge on specific component
- Display spinner next to stale data
- Disable actions during refetch
- Show progress bars for individual queries

### Global `IsFetching`:
- Top navigation bar loading indicator
- Global progress bar
- Prevent navigation during data sync
- Show "Syncing..." toast notification
- Network activity indicator

## Best Practices

### 1. **Don't Block UI During Background Fetch**

```csharp
// ✅ Good: Show data with refresh indicator
if (query.IsSuccess)
{
    if (query.IsFetchingBackground)
        Console.WriteLine("🔄 Refreshing...");
    
    DisplayData(query.Data); // Still show old data
}

// ❌ Bad: Block UI during background fetch
if (query.IsFetching)
{
    Console.WriteLine("Loading..."); // User sees loading screen even with data!
    return;
}
```

### 2. **Use Appropriate Indicators**

```csharp
// Initial load: Full loading screen
if (query.IsLoading)
{
    return LoadingScreen();
}

// Background fetch: Subtle indicator
if (query.IsFetchingBackground)
{
    ShowRefreshBadge();
}

// Show content
return ContentView(query.Data);
```

### 3. **Global Indicator for Navigation**

```csharp
// Prevent navigation while syncing
public bool CanNavigate => !_queryClient.IsFetching;

public void OnNavigate()
{
    if (_queryClient.IsFetching)
    {
        Console.WriteLine("⚠️ Please wait for data to finish syncing");
        return;
    }
    
    Navigate();
}
```

## Comparison with React Query

### React Query (TypeScript):
```typescript
function Todos() {
  const { status, data, error, isFetching } = useQuery({
    queryKey: ['todos'],
    queryFn: fetchTodos,
  })

  return status === 'pending' ? (
    <span>Loading...</span>
  ) : (
    <>
      {isFetching ? <div>Refreshing...</div> : null}
      <TodoList todos={data} />
    </>
  )
}

// Global indicator
function GlobalLoader() {
  const isFetching = useIsFetching()
  return isFetching ? <div>Loading...</div> : null
}
```

### SwrSharp (C#):
```csharp
public class TodosComponent
{
    private UseQuery<List<Todo>> _query;

    public void Render()
    {
        if (_query.Status == QueryStatus.Pending)
        {
            Console.WriteLine("Loading...");
        }
        else
        {
            if (_query.IsFetching)
                Console.WriteLine("Refreshing...");
            
            RenderTodoList(_query.Data);
        }
    }
}

// Global indicator
public class GlobalLoader
{
    private readonly QueryClient _client;
    
    public GlobalLoader(QueryClient client)
    {
        _client = client;
        _client.OnFetchingChanged += Render;
    }
    
    private void Render()
    {
        if (_client.IsFetching)
            Console.WriteLine("Loading...");
    }
}
```

**Key Differences**:
- React Query: `useIsFetching()` hook
- SwrSharp: `QueryClient.IsFetching` property + `OnFetchingChanged` event
- Both provide same functionality, adapted for platform
