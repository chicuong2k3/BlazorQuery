---
title: "Filters"
description: "Filtering queries"
order: 16
category: "Guides"
---

Some methods within SwrSharp accept a `QueryFilters` object.

## Query Filters

A query filter is an object with certain conditions to match a query with:

```csharp
// Invalidate all queries
queryClient.InvalidateQueries();

// Invalidate all queries that begin with "posts" in the key
queryClient.InvalidateQueries(new QueryFilters
{
    QueryKey = new("posts")
});

// Invalidate with exact match
queryClient.InvalidateQueries(new QueryFilters
{
    QueryKey = new("posts"),
    Exact = true
});

// Invalidate with custom predicate
queryClient.InvalidateQueries(new QueryFilters
{
    Predicate = key => key.Parts[0]?.ToString() == "posts"
});
```

A query filter object supports the following properties:

### `QueryKey`
```csharp
public QueryKey? QueryKey { get; set; }
```
Set this property to define a query key to match on. Uses prefix matching by default.

**Example:**
```csharp
// Matches "posts", "posts"/1, "posts"/list, etc.
new QueryFilters { QueryKey = new("posts") }
```

### `Exact`
```csharp
public bool Exact { get; set; }
```
If you don't want to search queries inclusively by query key, you can set `Exact = true` to return only the query with the exact query key you have passed.

**Example:**
```csharp
// Only matches exactly "posts", not "posts"/1
new QueryFilters
{
    QueryKey = new("posts"),
    Exact = true
}
```

### `Type`
```csharp
public QueryType Type { get; set; } = QueryType.All
```

Defaults to `QueryType.All`
- When set to `QueryType.Active` it will match active queries (have active observers/UseQuery instances).
- When set to `QueryType.Inactive` it will match inactive queries (no active observers).
- When set to `QueryType.All` it will match all queries.

**Example:**
```csharp
// Only match active queries
new QueryFilters { Type = QueryType.Active }

// Only match inactive queries
new QueryFilters { Type = QueryType.Inactive }
```

### `Stale`
```csharp
public bool? Stale { get; set; }
```

- When set to `true` it will match stale queries.
- When set to `false` it will match fresh queries.
- When `null` (default) it will match all queries.

Staleness is determined by comparing the cache entry's fetch time against the active query's configured `StaleTime`.

**Example:**
```csharp
// Only match stale queries
new QueryFilters { Stale = true }

// Only match fresh queries
new QueryFilters { Stale = false }
```

### `FetchStatus`
```csharp
public FetchStatus? FetchStatus { get; set; }
```

- When set to `FetchStatus.Fetching` it will match queries that are currently fetching.
- When set to `FetchStatus.Paused` it will match queries that wanted to fetch, but have been paused.
- When set to `FetchStatus.Idle` it will match queries that are not fetching.
- When `null` (default) it will match all queries.

**Example:**
```csharp
// Only match queries currently fetching
new QueryFilters { FetchStatus = FetchStatus.Fetching }

// Only match paused queries
new QueryFilters { FetchStatus = FetchStatus.Paused }

// Only match idle queries
new QueryFilters { FetchStatus = FetchStatus.Idle }
```

### `Predicate`
```csharp
public Func<QueryKey, bool>? Predicate { get; set; }
```
This predicate function is evaluated as a **final AND filter** after all other filter conditions (QueryKey, Type, Stale, FetchStatus). A query must pass all other filters first, then also pass the predicate to match.

**Example:**
```csharp
// Custom filtering logic
new QueryFilters
{
    Predicate = key => {
        if (key.Parts.Count < 2) return false;
        var id = key.Parts[1] as int?;
        return id > 100;
    }
}
```

## Combining Filters

You can combine multiple filter properties for precise query matching. All filters use **AND** logic — a query must satisfy every specified condition to match.

```csharp
// Match active, stale queries starting with "posts"
queryClient.InvalidateQueries(new QueryFilters
{
    QueryKey = new("posts"),
    Type = QueryType.Active,
    Stale = true
});

// Match inactive queries that are idle
queryClient.InvalidateQueries(new QueryFilters
{
    Type = QueryType.Inactive,
    FetchStatus = FetchStatus.Idle
});

// Prefix match with predicate (AND logic)
queryClient.InvalidateQueries(new QueryFilters
{
    QueryKey = new("todos"),
    Predicate = key => {
        if (key.Parts.Count < 2) return false;
        var id = key.Parts[1] as int?;
        return id.HasValue && id.Value > 1000;
    }
});
```

## Filter Matching Logic

The matching process follows these steps (all combined with AND logic):

1. **QueryKey Matching**: If `QueryKey` is specified:
   - If `Exact = true`: Must match exactly
   - If `Exact = false` (default): Prefix matching via `StartsWith`
   - If no `QueryKey` is specified, this step passes for all queries.

2. **Type Filtering**: If `Type` is not `All`:
   - `Active`: Only matches queries with active `UseQuery` observers
   - `Inactive`: Only matches queries with no active observers

3. **Staleness Filtering**: If `Stale` is specified:
   - `true`: Only matches stale queries (fetch time + staleTime has elapsed)
   - `false`: Only matches fresh queries

4. **FetchStatus Filtering**: If `FetchStatus` is specified:
   - Must match the query's current `FetchStatus` (`Fetching`, `Paused`, or `Idle`)

5. **Predicate**: If `Predicate` is specified, it is evaluated as a final AND filter after all above checks pass.

## Methods That Accept Filters

### `InvalidateQueries`
Marks matching queries as stale and triggers refetch for active ones.
```csharp
queryClient.InvalidateQueries(new QueryFilters
{
    QueryKey = new("posts")
});
```

### `CancelQueries`
Cancels ongoing fetches for matching queries.
```csharp
queryClient.CancelQueries(new QueryFilters
{
    QueryKey = new("posts")
});
```

### `RefetchQueries`
Triggers a refetch for matching active queries.
```csharp
queryClient.RefetchQueries(new QueryFilters
{
    QueryKey = new("posts")
});
```

### `RemoveQueries`
Removes matching queries from the cache entirely.
```csharp
queryClient.RemoveQueries(new QueryFilters
{
    QueryKey = new("posts")
});
```

### `ResetQueries`
Resets matching queries to their initial state — clears cached data and triggers refetch for active queries.
```csharp
queryClient.ResetQueries(new QueryFilters
{
    QueryKey = new("posts")
});
```

### `GetFetchingCount`
Returns the number of matching queries that are currently fetching.
```csharp
// Count all fetching queries
int count = queryClient.GetFetchingCount();

// Count fetching queries matching a filter
int postsCount = queryClient.GetFetchingCount(new QueryFilters
{
    QueryKey = new("posts")
});
```
