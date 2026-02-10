---
title: "Disabling Queries"
description: "Conditionally disable queries"
order: 9
category: "Guides"
---

# Disabling/Pausing Queries

If you ever want to disable a query from automatically running, you can use the `enabled: false` option.

When `enabled` is `false`:

- If the query has cached data, then the query will be initialized in the `Status == QueryStatus.Success` or `IsSuccess` state.
- If the query does not have cached data, then the query will start in the `Status == QueryStatus.Pending` and `FetchStatus == FetchStatus.Idle` state.
- The query will not automatically fetch on mount.
- The query will not automatically refetch in the background.
- The query will ignore query client `Invalidate` and `Refetch` calls that would normally result in the query refetching.
- `RefetchAsync()` returned from `UseQuery` can be used to manually trigger the query to fetch by temporarily enabling it.

## Basic Usage

```csharp
public class TodosComponent
{
    private readonly QueryClient _queryClient;
    private UseQuery<List<Todo>>? _todosQuery;

    public async Task InitializeAsync()
    {
        _todosQuery = new UseQuery<List<Todo>>(
            new QueryOptions<List<Todo>>(
                queryKey: new("todos"),
                queryFn: async ctx => await FetchTodoListAsync(),
                enabled: false // Disabled by default
            ),
            _queryClient
        );

        // Query won't execute automatically
        // User must manually trigger it
    }

    public async Task OnFetchButtonClickAsync()
    {
        if (_todosQuery == null) return;

        // Temporarily enable and fetch
        _todosQuery._queryOptions.Enabled = true;
        await _todosQuery.RefetchAsync();
        _todosQuery._queryOptions.Enabled = false; // Optional: disable again
    }

    private void RenderUI()
    {
        if (_todosQuery == null) return;

        if (_todosQuery.Data != null)
        {
            // Render todos
            foreach (var todo in _todosQuery.Data)
            {
                Console.WriteLine($"- {todo.Title}");
            }
        }
        else if (_todosQuery.IsError)
        {
            Console.WriteLine($"Error: {_todosQuery.Error?.Message}");
        }
        else if (_todosQuery.IsLoading)
        {
            Console.WriteLine("Loading...");
        }
        else
        {
            Console.WriteLine("Not ready... Click fetch to load data");
        }

        if (_todosQuery.IsFetching)
        {
            Console.WriteLine("Fetching...");
        }
    }
}
```

## Why Permanent Disabling is Not Recommended

Permanently disabling a query opts out of many great features that SwrSharp has to offer (like background refetches), and it's also not the idiomatic way. It takes you from the **declarative approach** (defining dependencies when your query should run) into an **imperative mode** (fetch whenever I click here).

Instead of permanent disabling, consider using **Lazy Queries** (see below).

## Lazy Queries

The `enabled` option can not only be used to permanently disable a query, but also to enable/disable it at a later time. A good example would be a filter form where you only want to fire off the first request once the user has entered a filter value:

```csharp
public class TodosComponent
{
    private readonly QueryClient _queryClient;
    private string _filter = string.Empty;
    private UseQuery<List<Todo>>? _todosQuery;

    public async Task InitializeAsync(string initialFilter = "")
    {
        _filter = initialFilter;
        CreateQuery();
    }

    private void CreateQuery()
    {
        _todosQuery = new UseQuery<List<Todo>>(
            new QueryOptions<List<Todo>>(
                queryKey: new("todos", _filter),
                queryFn: async ctx => await FetchTodosAsync(_filter),
                // Disabled as long as the filter is empty
                enabled: !string.IsNullOrEmpty(_filter)
            ),
            _queryClient
        );

        _todosQuery.OnChange += RenderUI;
    }

    public async Task OnApplyFilterAsync(string newFilter)
    {
        _filter = newFilter;

        // Recreate query with new filter
        _todosQuery?.Dispose();
        CreateQuery();

        // Applying the filter will enable and execute the query
        if (!string.IsNullOrEmpty(_filter))
        {
            await _todosQuery!.ExecuteAsync();
        }
    }

    private void RenderUI()
    {
        if (_todosQuery?.Data != null)
        {
            // Render filtered todos
            Console.WriteLine($"Showing {_todosQuery.Data.Count} todos");
        }
    }
}
```

## Dynamic Enable/Disable

You can dynamically change the `enabled` state based on application state:

```csharp
public class UserDashboardComponent
{
    private readonly QueryClient _queryClient;
    private string? _currentUserId;
    private UseQuery<UserData>? _userDataQuery;

    public async Task LoadUserAsync(string? userId)
    {
        _currentUserId = userId;

        _userDataQuery = new UseQuery<UserData>(
            new QueryOptions<UserData>(
                queryKey: new("userData", userId ?? "none"),
                queryFn: async ctx => await FetchUserDataAsync(_currentUserId!),
                // Only enabled when we have a userId
                enabled: !string.IsNullOrEmpty(_currentUserId)
            ),
            _queryClient
        );

        if (!string.IsNullOrEmpty(_currentUserId))
        {
            await _userDataQuery.ExecuteAsync();
        }
    }

    public async Task OnUserChangedAsync(string? newUserId)
    {
        _currentUserId = newUserId;

        // Update query options
        if (_userDataQuery != null)
        {
            _userDataQuery._queryOptions.Enabled = !string.IsNullOrEmpty(newUserId);

            if (!string.IsNullOrEmpty(newUserId))
            {
                await _userDataQuery.RefetchAsync();
            }
        }
    }
}
```

## IsLoading for Lazy Queries

Lazy queries will be in `Status: QueryStatus.Pending` right from the start because `Pending` means that there is no data yet. This is technically true, however, since we are not currently fetching any data (as the query is not _enabled_), it also means you likely cannot use this flag to show a loading spinner.

If you are using disabled or lazy queries, you can use the `IsLoading` flag instead. It's a derived flag that is computed from:

```csharp
public bool IsLoading => Status == QueryStatus.Pending && 
                         (FetchStatus == FetchStatus.Fetching || FetchStatus == FetchStatus.Paused);
```

So it will only be `true` if the query is currently fetching for the first time.

### Example: Distinguishing States

```csharp
var query = new UseQuery<Data>(
    new QueryOptions<Data>(
        queryKey: new("data"),
        queryFn: async ctx => await FetchDataAsync(),
        enabled: false
    ),
    queryClient
);

// Disabled query states:
// Status: Pending (no data yet)
// FetchStatus: Idle (not fetching)
// IsLoading: false (not fetching, even though pending)
// IsPending: true (no data)

Console.WriteLine($"Status: {query.Status}");           // Pending
Console.WriteLine($"FetchStatus: {query.FetchStatus}"); // Idle
Console.WriteLine($"IsLoading: {query.IsLoading}");     // false
Console.WriteLine($"IsPending: {query.IsPending}");     // true

// Enable and execute
query._queryOptions.Enabled = true;
var fetchTask = query.ExecuteAsync();

// Now fetching:
// Status: Pending
// FetchStatus: Fetching
// IsLoading: true (pending AND fetching)
// IsPending: true

await fetchTask;

// After fetch:
// Status: Success
// FetchStatus: Idle
// IsLoading: false
// IsPending: false
```

## Behavior with Cached Data

When a query is disabled but has cached data from a previous fetch:

```csharp
// First, fetch with enabled query
var query = new UseQuery<Data>(
    new QueryOptions<Data>(
        queryKey: new("data"),
        queryFn: async ctx => await FetchDataAsync(),
        enabled: true
    ),
    queryClient
);

await query.ExecuteAsync();

Console.WriteLine(query.Status);  // Success
Console.WriteLine(query.Data);    // <cached data>

// Now disable the query
query._queryOptions.Enabled = false;

// Query still has cached data
Console.WriteLine(query.Status);  // Success (has cached data)
Console.WriteLine(query.Data);    // <cached data>

// But won't refetch automatically
await query.ExecuteAsync();       // Does nothing (disabled)

// Cache remains available
Console.WriteLine(query.Data);    // <cached data>
```

## Ignore Invalidations When Disabled

When a query is disabled, it will ignore `Invalidate` and `Refetch` calls:

```csharp
var query = new UseQuery<Data>(
    new QueryOptions<Data>(
        queryKey: new("data"),
        queryFn: async ctx => await FetchDataAsync(),
        enabled: false
    ),
    queryClient
);

// This won't trigger a fetch (query is disabled)
queryClient.Invalidate(new QueryKey("data"));

// This also won't trigger a fetch
await query.RefetchAsync(); // Returns immediately, no fetch

// To manually fetch, you must enable first
query._queryOptions.Enabled = true;
await query.RefetchAsync(); // Now it fetches
```

## Complete Example: Manual Data Loading

```csharp
public class DataFetcherComponent : IDisposable
{
    private readonly QueryClient _queryClient;
    private UseQuery<List<Item>>? _itemsQuery;
    private bool _isManualMode = true;

    public DataFetcherComponent(QueryClient queryClient)
    {
        _queryClient = queryClient;
    }

    public async Task InitializeAsync()
    {
        _itemsQuery = new UseQuery<List<Item>>(
            new QueryOptions<List<Item>>(
                queryKey: new("items"),
                queryFn: async ctx => {
                    Console.WriteLine("Fetching items...");
                    return await FetchItemsAsync();
                },
                enabled: !_isManualMode, // Disabled in manual mode
                staleTime: TimeSpan.FromMinutes(5)
            ),
            _queryClient
        );

        _itemsQuery.OnChange += RenderUI;

        // In manual mode, user must click to fetch
        // In auto mode, fetch automatically
        if (!_isManualMode)
        {
            await _itemsQuery.ExecuteAsync();
        }
    }

    public async Task OnFetchButtonClickAsync()
    {
        if (_itemsQuery == null) return;

        // Temporarily enable for manual fetch
        var wasEnabled = _itemsQuery._queryOptions.Enabled;
        _itemsQuery._queryOptions.Enabled = true;

        try
        {
            await _itemsQuery.RefetchAsync();
        }
        finally
        {
            // Restore previous state
            _itemsQuery._queryOptions.Enabled = wasEnabled;
        }
    }

    public async Task ToggleModeAsync()
    {
        _isManualMode = !_isManualMode;
        
        if (_itemsQuery != null)
        {
            _itemsQuery._queryOptions.Enabled = !_isManualMode;

            // If switching to auto mode, fetch immediately
            if (!_isManualMode && _itemsQuery.Data == null)
            {
                await _itemsQuery.ExecuteAsync();
            }
        }
    }

    private void RenderUI()
    {
        if (_itemsQuery == null) return;

        Console.WriteLine("===== Data Fetcher =====");
        Console.WriteLine($"Mode: {(_isManualMode ? "Manual" : "Auto")}");
        Console.WriteLine($"Status: {_itemsQuery.Status}");
        Console.WriteLine($"IsLoading: {_itemsQuery.IsLoading}");
        Console.WriteLine($"IsFetching: {_itemsQuery.IsFetching}");

        if (_itemsQuery.Data != null)
        {
            Console.WriteLine($"Items: {_itemsQuery.Data.Count}");
        }
        else if (_itemsQuery.IsError)
        {
            Console.WriteLine($"Error: {_itemsQuery.Error?.Message}");
        }
        else
        {
            Console.WriteLine("No data. Click fetch to load.");
        }

        Console.WriteLine("=======================");
    }

    public void Dispose()
    {
        _itemsQuery?.Dispose();
    }
}
```

## Best Practices

### 1. **Prefer Lazy Queries Over Permanent Disabling**

```csharp
// ❌ Bad: Permanent disabling (imperative)
var query = new UseQuery<Data>(
    new QueryOptions<Data>(
        queryKey: new("data"),
        queryFn: async ctx => await FetchDataAsync(),
        enabled: false // Always disabled
    ),
    queryClient
);

// User must manually fetch every time
await ManualFetch();

// ✅ Good: Lazy query (declarative)
var query = new UseQuery<Data>(
    new QueryOptions<Data>(
        queryKey: new("data"),
        queryFn: async ctx => await FetchDataAsync(),
        enabled: HasRequiredParameters() // Depends on state
    ),
    queryClient
);

// Query automatically fetches when parameters are ready
```

### 2. **Use for Conditional Data Loading**

```csharp
// ✅ Good: Only fetch when needed
var premiumFeaturesQuery = new UseQuery<PremiumData>(
    new QueryOptions<PremiumData>(
        queryKey: new("premiumFeatures"),
        queryFn: async ctx => await FetchPremiumFeaturesAsync(),
        enabled: user.IsPremium // Only fetch for premium users
    ),
    queryClient
);
```

### 3. **Handle LoadingStates Properly**

```csharp
if (query.IsLoading)
{
    // First-time loading
    return LoadingSpinner();
}

if (!query._queryOptions.Enabled && query.Data == null)
{
    // Disabled and no data
    return PlaceholderMessage("Click to load data");
}

if (query.IsSuccess)
{
    // Has data
    return DataView(query.Data);
}
```

## Comparison with React Query

### React Query (TypeScript):
```typescript
function Todos() {
  const [filter, setFilter] = useState('')

  const { data } = useQuery({
    queryKey: ['todos', filter],
    queryFn: () => fetchTodos(filter),
    enabled: !!filter, // Disabled until filter is set
  })

  return (
    <div>
      <FiltersForm onApply={setFilter} />
      {data && <TodosTable data={data} />}
    </div>
  )
}
```

### SwrSharp (C#):
```csharp
public class TodosComponent
{
    private string _filter = string.Empty;
    private UseQuery<List<Todo>>? _query;

    public async Task OnApplyFilterAsync(string filter)
    {
        _filter = filter;

        _query = new UseQuery<List<Todo>>(
            new QueryOptions<List<Todo>>(
                queryKey: new("todos", _filter),
                queryFn: async ctx => await FetchTodosAsync(_filter),
                enabled: !string.IsNullOrEmpty(_filter)
            ),
            _queryClient
        );

        if (!string.IsNullOrEmpty(_filter))
        {
            await _query.ExecuteAsync();
        }

        RenderUI();
    }
}
```

**Key Differences**:
- React Query: Hook-based, automatic reactivity
- SwrSharp: Class-based, manual execution
- Both: Same `enabled` option behavior

## Note on skipToken

React Query provides `skipToken` as a type-safe alternative to `enabled: false`. SwrSharp doesn't have an equivalent because:
1. C# has nullable types and optional parameters
2. `enabled: condition` is already type-safe in C#
3. Query function is always required in constructor

If you need conditional query functions in C#:

```csharp
// Instead of skipToken, use conditional enabled
var query = new UseQuery<Data>(
    new QueryOptions<Data>(
        queryKey: new("data", param),
        queryFn: async ctx => await FetchDataAsync(param!), // Assume param is not null when enabled
        enabled: param != null
    ),
    queryClient
);
```
