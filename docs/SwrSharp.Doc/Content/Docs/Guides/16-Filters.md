---
title: "Filters"
description: "Guide for Filters in SwrSharp"
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

// Invalidate all inactive queries that begin with "posts"
queryClient.InvalidateQueries(new QueryFilters 
{ 
    QueryKey = new("posts"),
    Type = QueryType.Inactive
});

// Invalidate all active queries
queryClient.InvalidateQueries(new QueryFilters 
{ 
    Type = QueryType.Active
});

// Invalidate all stale queries that begin with "posts"
queryClient.InvalidateQueries(new QueryFilters 
{ 
    QueryKey = new("posts"),
    Stale = true
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
- When set to `QueryType.Active` it will match active queries (have active observers).
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

You can combine multiple filter properties for precise query matching:

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

// Complex filtering with predicate
queryClient.InvalidateQueries(new QueryFilters
{
    QueryKey = new("todos"),
    Stale = true,
    Predicate = key => {
        // Additional custom logic
        var metadata = key.Parts[1];
        return IsOlderThan(metadata, TimeSpan.FromHours(1));
    }
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

### Example 2: Clean Up Inactive Queries

```csharp
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

### Example 3: Force Refetch Active Queries

```csharp
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

The matching process follows these steps:

1. **QueryKey Matching**: If `QueryKey` is specified:
   - If `Exact = true`: Must match exactly
   - If `Exact = false` (default): Prefix matching

2. **Type Filtering**: If `Type` is specified:
   - `Active`: Query must have active observers
   - `Inactive`: Query must have no active observers
   - `All` (default): No filtering

3. **Staleness Filtering**: If `Stale` is specified:
   - `true`: Query must be stale
   - `false`: Query must be fresh
   - `null` (default): No filtering

4. **FetchStatus Filtering**: If `FetchStatus` is specified:
   - `Fetching`: Query must be currently fetching
   - `Paused`: Query must be paused
   - `Idle`: Query must be idle
   - `null` (default): No filtering

5. **Predicate**: If `Predicate` is specified:
   - Called as final filter on all queries that passed previous filters
   - Must return `true` to match

All filters are combined with **AND** logic - a query must satisfy ALL specified conditions to match.

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
// Remove inactive posts queries
queryClient.InvalidateQueries(new QueryFilters 
{ 
  QueryKey = new("posts"),
  Type = QueryType.Inactive
});

// Invalidate active queries
queryClient.InvalidateQueries(new QueryFilters 
{ 
  Type = QueryType.Active
});

// Filter by fetch status
queryClient.InvalidateQueries(new QueryFilters
{
  QueryKey = new("posts"),
  FetchStatus = FetchStatus.Idle
});

// Custom predicate
queryClient.InvalidateQueries(new QueryFilters
{
  Predicate = key => {
    if (key.Parts[0]?.ToString() != "todos") return false;
    // Additional logic based on key
    return true;
  }
});
```

---

## Summary

- ✅ `QueryFilters` for precise query matching
- ✅ `QueryKey` - prefix or exact matching
- ✅ `Type` - active/inactive/all
- ✅ `Stale` - fresh/stale filtering
- ✅ `FetchStatus` - fetching/paused/idle
- ✅ `Predicate` - custom matching logic
- ✅ Combine multiple filters with AND logic
- ✅ Flexible and powerful query selection

**Use filters for precise control over which queries to invalidate, refetch, or remove!** 🎯

