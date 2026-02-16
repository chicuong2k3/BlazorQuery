---
title: "Query Options"
description: "Query configuration options"
order: 5
category: "Guides"
---

## Overview

One of the best ways to share `QueryKey` and `QueryFn` between multiple places, yet keep them co-located to one another, is to use **factory methods** that return `QueryOptions<T>`. This pattern allows you to define all possible options for a query in one place with full type safety and reusability.

## Available Options

`QueryOptions<T>` supports the following properties:

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `QueryKey` | `QueryKey` | (required) | Unique identifier for the query |
| `QueryFn` | `Func<QueryFunctionContext, Task<T>>` | `null` | Function that fetches the data |
| `StaleTime` | `TimeSpan` | `TimeSpan.Zero` | How long data is considered fresh |
| `NetworkMode` | `NetworkMode` | `Online` | How to handle offline state |
| `RefetchOnReconnect` | `bool` | `true` | Refetch when network reconnects |
| `RefetchOnWindowFocus` | `bool` | `true` | Refetch when window gains focus |
| `RefetchInterval` | `TimeSpan?` | `null` | Polling interval |
| `Retry` | `int?` | `3` | Number of retries (matches React Query default) |
| `RetryInfinite` | `bool` | `false` | Retry indefinitely |
| `RetryFunc` | `Func<int, Exception, bool>` | `null` | Custom retry logic |
| `RetryDelay` | `TimeSpan?` | `null` | Fixed retry delay |
| `RetryDelayFunc` | `Func<int, TimeSpan>` | `null` | Custom retry delay |
| `MaxRetryDelay` | `TimeSpan?` | `30s` | Maximum retry delay |
| `Enabled` | `bool` | `true` | Whether query should execute |
| `Meta` | `IReadOnlyDictionary<string, object>?` | `null` | Custom metadata |
| `InitialData` | `T?` | `null` | Initial data (persisted to cache) |
| `InitialDataFunc` | `Func<T?>` | `null` | Lazy initial data |
| `InitialDataUpdatedAt` | `DateTime?` | `null` | Timestamp of initial data |
| `PlaceholderData` | `T?` | `null` | Placeholder (not cached) |
| `PlaceholderDataFunc` | `Func<T?, QueryOptions<T>?, T?>` | `null` | Dynamic placeholder |

## Basic Pattern

Instead of creating `QueryOptions` inline every time, create reusable factory methods:

```csharp
// Define a reusable query options factory
static QueryOptions<Group> GroupOptions(int id)
{
    return new QueryOptions<Group>(
        queryKey: new("groups", id),
        queryFn: async ctx => await FetchGroupAsync(id),
        staleTime: TimeSpan.FromSeconds(5)
    );
}

// Usage in multiple places:
var query1 = new UseQuery<Group>(GroupOptions(1), queryClient);
var query2 = new UseQuery<Group>(GroupOptions(5), queryClient);

// Can also be used with QueryClient
await queryClient.PrefetchQueryAsync(GroupOptions(23));
queryClient.SetQueryData(GroupOptions(42).QueryKey, newGroup);
```

## Advanced Examples

### With Multiple Parameters

```csharp
static QueryOptions<List<Todo>> TodoListOptions(string status, int page, int pageSize)
{
    return new QueryOptions<List<Todo>>(
        queryKey: new("todos", status, page, pageSize),
        queryFn: async ctx => {
            var (queryKey, signal) = ctx;
            var s = (string)queryKey[1]!;
            var p = (int)queryKey[2]!;
            var ps = (int)queryKey[3]!;
            return await FetchTodosAsync(s, p, ps, signal);
        },
        staleTime: TimeSpan.FromMinutes(5),
        retry: 3
    );
}

// Usage
var activeQuery = new UseQuery<List<Todo>>(TodoListOptions("active", 1, 10), client);
var doneQuery = new UseQuery<List<Todo>>(TodoListOptions("done", 1, 10), client);
```

### With Metadata

```csharp
static QueryOptions<User> UserOptions(int userId, bool includeDetails = false)
{
    return new QueryOptions<User>(
        queryKey: new("user", userId),
        queryFn: async ctx => {
            var (queryKey, signal, meta) = ctx;
            var id = (int)queryKey[1]!;
            var details = meta?.ContainsKey("includeDetails") == true;
            return await FetchUserAsync(id, details, signal);
        },
        staleTime: TimeSpan.FromMinutes(10),
        meta: includeDetails 
            ? new Dictionary<string, object> { { "includeDetails", true } }
            : null
    );
}

// Usage
var basicUser = new UseQuery<User>(UserOptions(1), client);
var detailedUser = new UseQuery<User>(UserOptions(1, includeDetails: true), client);
```

## Overriding Options

You can still override options at the component level while keeping the base configuration:

```csharp
// Base options
static QueryOptions<Group> GroupOptions(int id)
{
    return new QueryOptions<Group>(
        queryKey: new("groups", id),
        queryFn: async ctx => await FetchGroupAsync(id),
        staleTime: TimeSpan.FromSeconds(5)
    );
}

// Override specific options
var customQuery = new UseQuery<Group>(
    new QueryOptions<Group>(
        queryKey: GroupOptions(1).QueryKey,      // Reuse key
        queryFn: GroupOptions(1).QueryFn,        // Reuse function
        staleTime: TimeSpan.FromMinutes(1),      // Override staleTime
        retry: 5                                 // Add retry
    ),
    queryClient
);
```

## Summary

Using factory methods to create reusable `QueryOptions<T>` provides:
- Better organization and maintainability
- Easy refactoring
- Consistent query configuration
- Reduced boilerplate
