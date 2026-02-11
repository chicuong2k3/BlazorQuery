---
title: "Network Mode"
description: "Handling network states"
order: 3
category: "Guides"
---


# Overview

SwrSharp provides 3 Network Modes to control how **Queries** behave when there is no 
network connection. You can set the network mode:
- Per query individually via `QueryOptions<T>.NetworkMode`.
- Globally via `QueryClient.DefaultNetworkMode`.

By default, queries use the `Online` network mode.

# Network Mode: Online

In `Online` mode:
- Queries execute **only when online**.
- If offline, the query enters `Paused` state and waits for network to return.
- Three fetch statuses are available:
  - **Fetching**: the query function is actively executing.
  - **Paused**: the query is paused until network returns.
  - **Idle**: the query is neither fetching nor paused.

Convenience flags: `IsFetching` and `IsPaused`.

**Mid-fetch offline behavior:**

If a query is running and the network goes offline mid-fetch:
1. The current fetch is **cancelled** and the query enters `Paused` state.
2. Any pending retries are also **paused**.
3. Execution resumes automatically once the connection returns.
4. This is a **continue** operation, not a refetch — the query picks up from where it left off.
5. If the query was disposed while paused, it will not continue.

**Important**: For your query function to respect network changes mid-execution,
you must pass the `CancellationToken` to your HTTP calls:

```csharp
var query = new UseQuery<Data>(
    new QueryOptions<Data>(
        queryKey: new("data"),
        queryFn: async ctx => {
            var (queryKey, signal) = ctx;
            // Pass signal to HTTP client — this allows cancellation when going offline
            var response = await httpClient.GetAsync(url, signal);
            return await response.Content.ReadFromJsonAsync<Data>(signal);
        }
    ),
    queryClient
);
```

**Platform Note**: Unlike React Query (browser), .NET HTTP requests cannot be paused 
mid-flight. They must be cancelled (via `CancellationToken`) or complete normally. 
The pause/continue mechanism works at the **retry level** — pausing between retry attempts,
not during the actual fetch operation.

`RefetchOnReconnect` defaults to `true` — stale queries will refetch when network reconnects.

# Network Mode: Always

In `Always` mode:
- Queries always fetch, **ignoring network state**.
- Queries are never paused — `FetchStatus` is always `Fetching` or `Idle`, never `Paused`.
- Retries do not pause; the query will enter `Error` state if all retries fail.
- `RefetchOnReconnect` is automatically set to `false`.

Use this mode for queries that don't require network:
- Reading from local storage/cache
- Returning mock data via `Task.FromResult()`
- Working with local databases

```csharp
var query = new UseQuery<Config>(
    new QueryOptions<Config>(
        queryKey: new("local-config"),
        queryFn: async ctx => await LoadFromLocalStorageAsync(),
        networkMode: NetworkMode.Always  // Won't pause even if offline
    ),
    queryClient
);
```

# Network Mode: OfflineFirst

`OfflineFirst` mode is a hybrid between `Online` and `Always`:
- The **first fetch attempt** executes regardless of network state.
- If the first attempt fails and network is offline, retries will **pause** (like `Online` mode).
- Useful for offline-first scenarios where data might be cached:
  - Service worker caching
  - HTTP cache (browser)
  - Local database with sync

```csharp
var query = new UseQuery<Data>(
    new QueryOptions<Data>(
        queryKey: new("cached-data"),
        queryFn: async ctx => await FetchWithCacheFallback(),
        networkMode: NetworkMode.OfflineFirst
    ),
    queryClient
);
// First attempt runs immediately
// If cache hit: success!
// If cache miss + offline: pauses retries until online
```

# Loading vs Fetching

SwrSharp distinguishes between **logical loading** and **active fetching**:

| Property | Meaning | Equivalent |
|----------|---------|------------|
| `IsFetching` | Query function is actively executing | `FetchStatus == Fetching` |
| `IsPaused` | Query is paused waiting for network | `FetchStatus == Paused` |
| `IsLoading` | First load in progress (no data yet) | `IsPending && (IsFetching || IsPaused)` |

**Key insight**: `IsLoading` is `true` when there's no data yet AND the query is either 
actively fetching or paused due to network conditions.

# Not Yet Implemented

> **Mutations**: SwrSharp does not yet implement Mutations. Network mode currently only 
> applies to Queries. When Mutations are added, they will support the same network modes.
>
> To customize online detection, implement `IOnlineManager` and pass it to `QueryClient`.
