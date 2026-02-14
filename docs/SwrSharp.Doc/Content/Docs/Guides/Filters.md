---
title: "Filters"
description: "Filtering queries"
order: 16
category: "Guides"
---

# Filters

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

> **Note**: `Type`, `Stale`, and `FetchStatus` filters exist as properties on `QueryFilters` but are **not yet implemented** in the matching logic. See sections below for details.

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

### `Type` (Not Yet Implemented)
```csharp
public QueryType Type { get; set; } = QueryType.All
```

> **Note**: This filter property exists on `QueryFilters` but is **not yet implemented** in the matching logic. Currently, all queries are treated as `QueryType.All` regardless of this setting. Active/Inactive tracking requires observer registration which is planned for a future release.

Defaults to `QueryType.All`
- When set to `QueryType.Active` it will match active queries (have active observers).
- When set to `QueryType.Inactive` it will match inactive queries (no active observers).
- When set to `QueryType.All` it will match all queries.

**Example (planned behavior):**
```csharp
// Only match active queries
new QueryFilters { Type = QueryType.Active }

// Only match inactive queries
new QueryFilters { Type = QueryType.Inactive }
```

### `Stale` (Not Yet Implemented)
```csharp
public bool? Stale { get; set; }
```

> **Note**: This filter property exists on `QueryFilters` but is **not yet implemented** in the matching logic. Currently, this setting has no effect. Staleness-based filtering requires access to query state (fetch timestamps and staleTime configuration) which is planned for a future release.

- When set to `true` it will match stale queries.
- When set to `false` it will match fresh queries.
- When `null` (default) it will match all queries.

**Example (planned behavior):**
```csharp
// Only match stale queries
new QueryFilters { Stale = true }

// Only match fresh queries
new QueryFilters { Stale = false }
```

### `FetchStatus` (Not Yet Implemented)
```csharp
public FetchStatus? FetchStatus { get; set; }
```

> **Note**: This filter property exists on `QueryFilters` but is **not yet implemented** in the matching logic. Currently, this setting has no effect. FetchStatus-based filtering requires access to active query instances which is planned for a future release.

- When set to `FetchStatus.Fetching` it will match queries that are currently fetching.
- When set to `FetchStatus.Paused` it will match queries that wanted to fetch, but have been paused.
- When set to `FetchStatus.Idle` it will match queries that are not fetching.
- When `null` (default) it will match all queries.

**Example (planned behavior):**
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
This predicate function will be used as a final filter on all matching queries. If no other filters are specified, this function will be evaluated against every query in the cache.

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

You can combine multiple filter properties for precise query matching.

> **Note**: Currently only `QueryKey`, `Exact`, and `Predicate` are functional. `Type`, `Stale`, and `FetchStatus` filters are planned for a future release.

**Currently working combinations:**
```csharp
// Prefix match with predicate
queryClient.InvalidateQueries(new QueryFilters
{
    QueryKey = new("todos"),
    Predicate = key => {
        // Additional custom logic
        if (key.Parts.Count < 2) return false;
        var id = key.Parts[1] as int?;
        return id.HasValue && id.Value > 1000;
    }
});

// Exact match
queryClient.InvalidateQueries(new QueryFilters
{
    QueryKey = new("todos"),
    Exact = true
});
```

**Planned combinations (not yet implemented):**
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
```

## Complete Examples

### Example 1: Invalidate Stale Posts

```csharp
public class BlogService
{
    private readonly QueryClient _queryClient;

    public async Task RefreshStalePostsAsync()
    {
        // Invalidate all stale posts queries
        _queryClient.InvalidateQueries(new QueryFilters
        {
            QueryKey = new("posts"),
            Stale = true
        });

        await Task.Delay(100); // Wait for refetch
    }
}
```

### Example 2: Clean Up Inactive Queries (Planned)

> **Note**: `QueryType.Active`/`Inactive` filtering is not yet implemented. Currently all queries are treated as matching regardless of this setting.

```csharp
// Planned behavior (not yet functional):
public class CacheManager
{
    private readonly QueryClient _queryClient;

    public void CleanUpInactiveQueries()
    {
        // Remove all inactive queries
        _queryClient.InvalidateQueries(new QueryFilters
        {
            Type = QueryType.Inactive
        });
    }

    public void CleanUpInactivePostQueries()
    {
        // Remove only inactive post queries
        _queryClient.InvalidateQueries(new QueryFilters
        {
            QueryKey = new("posts"),
            Type = QueryType.Inactive
        });
    }
}
```

### Example 3: Force Refetch Active Queries (Planned)

> **Note**: `QueryType.Active` filtering is not yet implemented.

```csharp
// Planned behavior (not yet functional):
public class DataSyncService
{
    private readonly QueryClient _queryClient;

    public async Task SyncAllActiveDataAsync()
    {
        // Invalidate all currently active queries
        // These will automatically refetch
        _queryClient.InvalidateQueries(new QueryFilters
        {
            Type = QueryType.Active
        });

        Console.WriteLine("All active queries are refreshing...");
    }
}
```

### Example 4: Advanced Filtering

```csharp
public class AdvancedQueryManager
{
    private readonly QueryClient _queryClient;

    public void InvalidateOldTodoQueries()
    {
        _queryClient.InvalidateQueries(new QueryFilters
        {
            QueryKey = new("todos"),
            Predicate = key => {
                // Only invalidate todos with id > 1000
                if (key.Parts.Count < 2) return false;
                
                var id = key.Parts[1] as int?;
                return id.HasValue && id.Value > 1000;
            }
        });
    }

    public void InvalidateQueriesForUser(int userId)
    {
        _queryClient.InvalidateQueries(new QueryFilters
        {
            Predicate = key => {
                // Check if query key contains user info
                foreach (var part in key.Parts)
                {
                    if (part is int id && id == userId)
                        return true;
                    
                    // Check anonymous objects
                    var userIdProp = part?.GetType().GetProperty("userId");
                    if (userIdProp != null)
                    {
                        var value = userIdProp.GetValue(part) as int?;
                        if (value == userId)
                            return true;
                    }
                }
                return false;
            }
        });
    }
}
```

## Filter Matching Logic

The matching process currently follows these steps:

1. **Predicate** (if specified): Custom predicate is evaluated first and takes full control of matching.

2. **QueryKey Matching** (if no Predicate): If `QueryKey` is specified:
   - If `Exact = true`: Must match exactly
   - If `Exact = false` (default): Prefix matching via `StartsWith`

3. **No QueryKey**: If no `QueryKey` is specified and no `Predicate`, matches all queries.

> **Not yet implemented**: The following filter steps are planned for a future release:
> - **Type Filtering** (`Active`/`Inactive`/`All`) - requires observer tracking
> - **Staleness Filtering** (`Stale = true/false`) - requires access to query fetch timestamps
> - **FetchStatus Filtering** (`Fetching`/`Paused`/`Idle`) - requires access to active query instances
>
> When implemented, all filters will be combined with **AND** logic - a query must satisfy ALL specified conditions to match.

## Comparison with React Query

### React Query (TypeScript):
```typescript
// Remove inactive posts queries
queryClient.removeQueries({ 
  queryKey: ['posts'], 
  type: 'inactive' 
})

// Refetch active queries
await queryClient.refetchQueries({ type: 'active' })

// Filter by fetch status
queryClient.invalidateQueries({
  queryKey: ['posts'],
  fetchStatus: 'idle'
})

// Custom predicate
queryClient.invalidateQueries({
  predicate: (query) => 
    query.queryKey[0] === 'todos' && query.state.data?.length > 0
})
```

### SwrSharp (C#):
```csharp
// Prefix match (working)
queryClient.InvalidateQueries(new QueryFilters
{
  QueryKey = new("posts")
});

// Custom predicate (working)
queryClient.InvalidateQueries(new QueryFilters
{
  Predicate = key => {
    if (key.Parts[0]?.ToString() != "todos") return false;
    // Additional logic based on key
    return true;
  }
});

// Type/Stale/FetchStatus filters (not yet implemented):
// queryClient.InvalidateQueries(new QueryFilters
// {
//   QueryKey = new("posts"),
//   Type = QueryType.Inactive
// });
```
