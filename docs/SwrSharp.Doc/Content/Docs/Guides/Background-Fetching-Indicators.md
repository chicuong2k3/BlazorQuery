---
title: "Background Fetching"
description: "Background refetch indicators"
order: 8
category: "Guides"
---

# Background Fetching Indicators

A query's `Status == QueryStatus.Pending` state is sufficient to show the initial hard-loading state for a query, but sometimes you may want to display an additional indicator that a query is refetching in the background. To do this, queries provide you with an `IsFetching` boolean that you can use to show that it's in a fetching state, regardless of the state of the `Status` variable.

## Individual Query Fetching Indicator

Use the `IsFetching` property to show background refetch indicators. Subscribe to `OnChange` to react to state changes:

```csharp
var todosQuery = new UseQuery<List<Todo>>(
    new QueryOptions<List<Todo>>(
        queryKey: new("todos"),
        queryFn: async ctx => await FetchTodosAsync()
    ),
    queryClient
);

todosQuery.OnChange += () =>
{
    if (todosQuery.Status == QueryStatus.Pending)
    {
        // Initial loading — no data yet
        // Show loading spinner
    }
    else if (todosQuery.Status == QueryStatus.Error)
    {
        // Error: todosQuery.Error?.Message
    }
    else
    {
        // Success — show data
        if (todosQuery.IsFetching)
        {
            // Background refetch in progress — show subtle indicator
            // "Refreshing..."
        }

        // Display todosQuery.Data
    }
};

_ = todosQuery.ExecuteAsync();
```

## IsFetching vs IsFetchingBackground

SwrSharp provides two related properties:

### `IsFetching`
- `true` when query is actively fetching (any fetch)
- Includes initial loads and background refetches
- Equivalent to `FetchStatus == FetchStatus.Fetching`

### `IsFetchingBackground`
- `true` only when refetching with existing data
- `false` during initial load (no data yet)
- Useful to distinguish background refetch from initial load

```csharp
query.OnChange += () =>
{
    if (query.IsLoading)
    {
        // First load — no data yet, fetching in progress
        // Show full loading screen
    }
    else if (query.IsFetchingBackground)
    {
        // Has data, fetching new data in background
        // Show subtle "Updating..." indicator while displaying current data
    }
    else if (query.IsSuccess)
    {
        // Data is fresh, not fetching
        // Show data normally
    }
};
```

## Example: Loading States

```csharp
var query = new UseQuery<List<Item>>(
    new QueryOptions<List<Item>>(
        queryKey: new("items"),
        queryFn: async ctx => await FetchItemsAsync(),
        staleTime: TimeSpan.FromSeconds(30)
    ),
    queryClient
);

query.OnChange += () =>
{
    // Initial loading (no data yet)
    if (query.IsLoading)
    {
        // "Loading data for the first time..."
        return;
    }

    // Error state
    if (query.IsError)
    {
        // "Error: {query.Error?.Message}"
        return;
    }

    // Success with data
    if (query.IsSuccess && query.Data != null)
    {
        // Background refetch indicator
        if (query.IsFetchingBackground)
        {
            // "Refreshing data in background..."
        }

        // "Loaded {query.Data.Count} items"
        // Display query.Data
    }
};

_ = query.ExecuteAsync();
```

## Global Background Fetching Indicator

If you want to show a global loading indicator when **any** queries are fetching (including in the background), use `QueryClient.IsFetching` and the `OnFetchingChanged` event:

```csharp
var queryClient = new QueryClient();

queryClient.OnFetchingChanged += () =>
{
    if (queryClient.IsFetching)
    {
        // Show global spinner or progress bar
        // "Syncing data..."
    }
    else
    {
        // Hide global spinner
        // "All data up to date"
    }
};
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
