---
title: "Disabling/Pausing Queries"
description: "Conditionally disable queries"
order: 9
category: "Guides"
---

If you ever want to disable a query from automatically running, you can use the `enabled: false` option.

When `enabled` is `false`:

- If the query has cached data, then the query will be initialized in the `Status == QueryStatus.Success` or `IsSuccess` state.
- If the query does not have cached data, then the query will start in the `Status == QueryStatus.Pending` and `FetchStatus == FetchStatus.Idle` state.
- The query will not automatically fetch on mount.
- The query will not automatically refetch in the background.
- The query will ignore query client `Invalidate` and `Refetch` calls that would normally result in the query refetching.
- `RefetchAsync()` returned from `UseQuery` can be used to manually trigger the query to fetch. It bypasses the `enabled` check.


## Why Permanent Disabling is Not Recommended

Permanently disabling a query opts out of many great features that SwrSharp has to offer (like background refetches), and it's also not the idiomatic way. It takes you from the **declarative approach** (defining dependencies when your query should run) into an **imperative mode** (fetch whenever I click here).

Instead of permanent disabling, consider using **Lazy Queries** (see below).

## Lazy Queries

The `enabled` option can not only be used to permanently disable a query, but also to enable/disable it at a later time. A good example would be a filter form where you only want to fire off the first request once the user has entered a filter value:

```csharp
string filter = string.Empty;
UseQuery<List<Todo>>? todosQuery = null;

void CreateQuery()
{
    todosQuery?.Dispose();

    todosQuery = new UseQuery<List<Todo>>(
        new QueryOptions<List<Todo>>(
            queryKey: new("todos", filter),
            queryFn: async ctx => await FetchTodosAsync(filter),
            // Disabled as long as the filter is empty
            enabled: !string.IsNullOrEmpty(filter)
        ),
        queryClient
    );

    todosQuery.OnChange += () =>
    {
        // Notify UI: todosQuery.Data, todosQuery.IsLoading, etc.
    };

    // Only fetches if enabled (filter is non-empty)
    _ = todosQuery.ExecuteAsync();
}

void OnApplyFilter(string newFilter)
{
    filter = newFilter;
    // Recreate query with new filter — will auto-fetch if filter is non-empty
    CreateQuery();
}
```

## Dynamic Enable/Disable

You can dynamically change the `enabled` state on an existing query using the `Options.Enabled` property:

```csharp
var options = new QueryOptions<List<Item>>(
    queryKey: new("items"),
    queryFn: async ctx => await FetchItemsAsync(),
    enabled: false,
    refetchInterval: TimeSpan.FromSeconds(30),
    refetchOnWindowFocus: true
);

var query = new UseQuery<List<Item>>(options, queryClient);

query.OnChange += () =>
{
    // Notify UI
};

// Query is disabled — ExecuteAsync, polling, and window focus refetch are all inactive
_ = query.ExecuteAsync(); // Does nothing

// Later, enable the query:
options.Enabled = true;
_ = query.ExecuteAsync(); // Now fetches! Polling and window focus refetch also activate.

// Disable again:
options.Enabled = false;
// Polling stops, invalidations are ignored, ExecuteAsync becomes a no-op
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

// Enable and execute
query.Options.Enabled = true;
_ = query.ExecuteAsync();

// While fetching:
// Status: Pending
// FetchStatus: Fetching
// IsLoading: true (pending AND fetching)

// After fetch completes (observed via OnChange):
// Status: Success
// FetchStatus: Idle
// IsLoading: false
// IsPending: false
```

## Behavior with Cached Data

When a query is disabled but has cached data from a previous fetch, it retains the cached data:

```csharp
var options = new QueryOptions<Data>(
    queryKey: new("data"),
    queryFn: async ctx => await FetchDataAsync()
);

var query = new UseQuery<Data>(options, queryClient);

query.OnChange += () =>
{
    // After first fetch completes:
    // query.Status == Success
    // query.Data == <fetched data>

    // If we then disable:
    // query.Status remains Success (has cached data)
    // query.Data still available
    // But auto-refetch (polling, window focus, invalidation) won't trigger
};

_ = query.ExecuteAsync();

// Later, disable the query
options.Enabled = false;

// Data is still accessible
// query.Data == <cached data>

// ExecuteAsync is now a no-op
_ = query.ExecuteAsync(); // Does nothing

// But data remains in cache
```

## Ignore Invalidations When Disabled

When a query is disabled, it will ignore `InvalidateQueries` calls from the `QueryClient`. However, `RefetchAsync()` called directly on the query instance will still work:

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
queryClient.InvalidateQueries(new QueryFilters { QueryKey = new QueryKey("data") });

// But RefetchAsync() bypasses the enabled check (matches React Query behavior)
_ = query.RefetchAsync(); // This WILL fetch!
```
