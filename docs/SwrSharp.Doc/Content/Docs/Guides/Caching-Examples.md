---
title: "Caching Examples"
description: "Understanding the SwrSharp caching lifecycle"
order: 19
category: "Guides"
---

# Caching Examples

This guide illustrates the caching lifecycle in SwrSharp, including cache hits, background refetching, and stale data handling.

## Basic Example

Let's assume we are using the default `staleTime` of `0` (data is immediately stale).

1. A new `UseQuery` for `["todos"]` mounts.
   - Since no other queries have been made with the `["todos"]` key, this query will show a loading state and make a network request to fetch the data.
   - When the network request completes, the returned data is cached under the `["todos"]` key.
   - The data is immediately marked as stale (default `staleTime` is `0`).

2. A second `UseQuery` for `["todos"]` mounts elsewhere.
   - The cache already has data for `["todos"]`, so that data is **immediately returned** from the cache.
   - A background refetch is triggered because the data is stale.
   - When the background refetch completes, both query instances are updated with the fresh data.

3. Both query instances unmount.
   - The cached data remains in the `QueryClient` cache and can be reused by future queries.

4. A new `UseQuery` for `["todos"]` mounts later.
   - The cached data is immediately available and returned.
   - A background refetch runs to ensure freshness.

```csharp
// First query - triggers network request
var query1 = new UseQuery<Todo[]>(new QueryOptions<Todo[]>(
    queryKey: new QueryKey("todos"),
    queryFn: async ctx => await FetchTodos(ctx.Signal)
), queryClient);

// Second query (same key) - gets cached data instantly, refetches in background
var query2 = new UseQuery<Todo[]>(new QueryOptions<Todo[]>(
    queryKey: new QueryKey("todos"),
    queryFn: async ctx => await FetchTodos(ctx.Signal)
), queryClient);
```

## Stale Time Example

With a non-zero `staleTime`, queries won't trigger background refetches until the data becomes stale:

```csharp
var query = new UseQuery<Todo[]>(new QueryOptions<Todo[]>(
    queryKey: new QueryKey("todos"),
    queryFn: async ctx => await FetchTodos(ctx.Signal)
)
{
    StaleTime = TimeSpan.FromMinutes(5) // Data is fresh for 5 minutes
}, queryClient);
```

**Timeline:**
1. `t=0`: Query fetches data, caches it, data is **fresh**
2. `t=2min`: Another component mounts with same key — gets cached data, **no refetch** (still fresh)
3. `t=5min`: Data becomes **stale**
4. `t=6min`: Another component mounts — gets cached data, triggers **background refetch** (stale)

## Cache Sharing

All queries with the same key share the same cache entry. When any one of them refetches, all instances receive the updated data:

```csharp
// Component A
var queryA = new UseQuery<User>(new QueryOptions<User>(
    queryKey: new QueryKey("user", 1),
    queryFn: async ctx => await FetchUser(1, ctx.Signal)
), queryClient);

// Component B (different component, same query key)
var queryB = new UseQuery<User>(new QueryOptions<User>(
    queryKey: new QueryKey("user", 1),
    queryFn: async ctx => await FetchUser(1, ctx.Signal)
), queryClient);

// When queryA refetches, queryB also receives the new data
// When queryB refetches, queryA also receives the new data
```

## Cache Invalidation Lifecycle

When a query is invalidated, here's what happens:

1. The cache entry is removed
2. If the query is currently being observed (active), it automatically refetches
3. The query transitions to a loading/fetching state during the refetch

```csharp
// Invalidate a specific query
queryClient.InvalidateQueries(new QueryFilters
{
    QueryKey = new QueryKey("todos")
});

// Invalidate after a mutation
var mutation = new UseMutation<Todo, CreateTodoInput>(
    new MutationOptions<Todo, CreateTodoInput>
    {
        MutationFn = async input => await CreateTodo(input),
        OnSuccess = async (data, variables, onMutateResult, context) =>
        {
            // Invalidate to refetch with the new todo included
            context.Client.InvalidateQueries(new QueryFilters
            {
                QueryKey = new QueryKey("todos")
            });
        }
    },
    queryClient
);
```

## Prefetching and the Cache

You can populate the cache before a query is needed using `PrefetchQueryAsync`:

```csharp
// Prefetch data (e.g., on hover or route change)
await queryClient.PrefetchQueryAsync(new QueryOptions<Todo[]>(
    queryKey: new QueryKey("todos"),
    queryFn: async ctx => await FetchTodos(ctx.Signal)
));

// Later, when the query mounts, data is instantly available from cache
var query = new UseQuery<Todo[]>(new QueryOptions<Todo[]>(
    queryKey: new QueryKey("todos"),
    queryFn: async ctx => await FetchTodos(ctx.Signal)
), queryClient);

// query.Data is immediately available!
```

## Manual Cache Updates

You can directly read and write cache entries without triggering refetches:

```csharp
// Read from cache
var todos = queryClient.GetQueryData<List<Todo>>(new QueryKey("todos"));

// Write to cache (e.g., after a mutation returns updated data)
queryClient.SetQueryData(new QueryKey("todo", 5), updatedTodo);

// This is useful for optimistic updates and avoiding redundant network calls
```

## Cache Cleanup

SwrSharp does not currently implement automatic garbage collection. Cache entries persist until explicitly removed:

```csharp
// Remove a specific cache entry
queryClient.Invalidate(new QueryKey("todos"));

// Clear all cache entries (typically on logout or app shutdown)
queryClient.Dispose();
```

> **Note**: Automatic garbage collection with configurable `gcTime` is planned for a future release.

## Key Takeaways

| Concept | Behavior |
|---|---|
| **Cache hit** | Queries with the same key share cached data instantly |
| **Stale data** | Stale queries trigger background refetches on mount, window focus, or reconnect |
| **Fresh data** | Fresh queries (within `staleTime`) do not refetch |
| **Invalidation** | Removes cache and triggers refetch for active queries |
| **Prefetching** | Populates cache before a query mounts |
| **Manual updates** | `SetQueryData` updates cache without network requests |
| **Cache lifetime** | Entries persist until invalidated or `QueryClient` is disposed |
