---
title: "Query Concepts"
description: "Core concepts of SwrSharp"
order: 1
category: "Concepts"
---


# Query Concepts

## What is a Query?

A query is a declarative description of an asynchronous resource that can be fetched, cached, and synchronized. It's identified by a unique query key and uses a query function to fetch data.

## Query States

Every query has three independent states:

### `Status`
Represents whether the query has data and/or error:

- **`Pending`**: No data and no error (first load)
- **`Error`**: Has error (even if stale data exists)
- **`Success`**: Has data and no error

```csharp
if (query.Status == QueryStatus.Pending)
    display loading state
else if (query.Status == QueryStatus.Error)
    display error state
else
    display data
```

### `FetchStatus`
Represents whether the query is actively fetching data:

- **`Idle`**: Not fetching
- **`Fetching`**: Actively fetching data
- **`Paused`**: Fetching paused due to network offline mode

```csharp
if (query.IsFetching)
    show refresh indicator
```

## Query Lifecycle

```
Initial Load
    ↓
Pending + Fetching
    ↓
Success + Idle
    ↓ (on refetch trigger)
Success + Fetching (background refetch)
    ↓
Success + Idle
```

## Cache Duration

Query data is cached until:

1. **Marked as stale** by `staleTime` — stale queries refetch on triggers like window focus
2. **Invalidated manually** by `InvalidateQueries()` — marks queries for immediate refetch
3. **Removed explicitly** by `Invalidate(key)` or `Dispose()` — clears cache entries

> **Note**: SwrSharp does not yet implement automatic garbage collection (`gcTime`). Cache entries persist until manually removed or the `QueryClient` is disposed.

## Fetching vs Loading

- **`IsLoading`**: `Status == Pending && IsFetching` (first load in progress)
- **`IsFetching`**: Actively fetching (includes background refetches)

These are different! You can be fetching without loading (background refetch).

## Network Awareness

SwrSharp is network-aware and handles offline scenarios:

- **Online Mode**: Pauses when offline, resumes on reconnect
- **Always Mode**: Ignores network status
- **OfflineFirst Mode**: Tries once, then pauses if offline

## Query Keys and Caching

Query keys are like cache keys. Identical keys share the same cache:

```csharp
// These all use the same cache
UseQuery(new QueryKey("todos"), FetchTodos);
UseQuery(new QueryKey("todos"), FetchTodos);

// Different cache (different dependencies)
UseQuery(new QueryKey("todos", userId), FetchUserTodos);
```

## Stale vs Invalid

- **Stale**: Data exists but is considered outdated
- **Invalid**: Data is explicitly marked for refetch

Stale queries refetch in the background on certain triggers. Invalid queries refetch immediately.
