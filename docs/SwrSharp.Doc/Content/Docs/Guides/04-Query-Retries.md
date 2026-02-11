---
title: "Query Retries"
description: "Configuring retry behavior"
order: 4
category: "Guides"
---


# Query Retries in SwrSharp

When a query fails (the query function throws an exception), **SwrSharp** can automatically 
retry the query based on the retry configuration in `QueryOptions<T>`.

## Retry Options

| Option | Type | Description |
|--------|------|-------------|
| `Retry` | `int?` | Number of retries after initial attempt. Default: `3` |
| `RetryInfinite` | `bool` | If `true`, retry indefinitely until success |
| `RetryFunc` | `Func<int, Exception, bool>` | Custom retry logic based on attempt index and error |
| `RetryDelay` | `TimeSpan?` | Fixed delay between retries |
| `RetryDelayFunc` | `Func<int, TimeSpan>` | Custom delay function based on attempt index |
| `MaxRetryDelay` | `TimeSpan?` | Maximum delay cap (default: 30 seconds) |

**Examples:**

```csharp
// Default behavior: 3 retries (4 total attempts: 1 initial + 3 retries)
var query = new UseQuery<string>(
    new QueryOptions<string>(
        queryKey: new("data"),
        queryFn: async ctx => await FetchDataAsync()
        // Retry defaults to 3
    ),
    queryClient
);

// Disable retries
var query = new UseQuery<string>(
    new QueryOptions<string>(
        queryKey: new("data"),
        queryFn: async ctx => await FetchDataAsync(),
        retry: 0  // No retries — fails immediately on error
    ),
    queryClient
);

// Custom retry count: 5 retries (6 total attempts)
var query = new UseQuery<string>(
    new QueryOptions<string>(
        queryKey: new("data"),
        queryFn: async ctx => await FetchDataAsync(),
        retry: 5
    ),
    queryClient
);

// Infinite retries until success
var query = new UseQuery<string>(
    new QueryOptions<string>(
        queryKey: new("data"),
        queryFn: async ctx => await FetchDataAsync(),
        retryInfinite: true
    ),
    queryClient
);

// Custom retry logic — only retry on specific errors
var query = new UseQuery<string>(
    new QueryOptions<string>(
        queryKey: new("data"),
        queryFn: async ctx => await FetchDataAsync(),
        retryFunc: (attemptIndex, error) => {
            // attemptIndex: 0 = first retry, 1 = second retry, etc.
            // Don't retry on 404 errors
            if (error is HttpRequestException { StatusCode: HttpStatusCode.NotFound })
                return false;
            // Retry up to 5 times for other errors
            return attemptIndex < 5;
        }
    ),
    queryClient
);
```


## FailureReason Property

During retry attempts, the error from each failed attempt is available via the `FailureReason` property.
After the last retry attempt fails, this error becomes the `Error` property.

```csharp
// During retries — FailureReason shows the current error
if (query.FailureReason != null && query.Error == null)
{
    Console.WriteLine($"Attempt failed, retrying: {query.FailureReason.Message}");
}

// After all retries exhausted — Error is set
if (query.Error != null)
{
    Console.WriteLine($"Query failed: {query.Error.Message}");
}
```

# Retry Delay

Retries are not immediate — a backoff delay is applied between attempts.

**Default behavior (exponential backoff):**
- First retry: **1000ms** (1 second)
- Second retry: **2000ms** (2 seconds)  
- Third retry: **4000ms** (4 seconds)
- And so on: `1000 * 2^attemptIndex`
- Maximum delay capped at **30 seconds** (configurable via `MaxRetryDelay`)

```csharp
// Default exponential backoff
var query = new UseQuery<string>(
    new QueryOptions<string>(
        queryKey: new("data"),
        queryFn: async ctx => await FetchDataAsync(),
        retry: 5  // Delays: 1s, 2s, 4s, 8s, 16s
    ),
    queryClient
);

// Custom max delay
var query = new UseQuery<string>(
    new QueryOptions<string>(
        queryKey: new("data"),
        queryFn: async ctx => await FetchDataAsync(),
        retry: 10,
        maxRetryDelay: TimeSpan.FromSeconds(10)  // Cap at 10 seconds
    ),
    queryClient
);
```

# Custom Retry Delay

Provide a custom delay function via `RetryDelayFunc`:

```csharp
// Custom delay logic
var query = new UseQuery<string>(
    new QueryOptions<string>(
        queryKey: new("data"),
        queryFn: async ctx => await FetchDataAsync(),
        retry: 5,
        retryDelayFunc: attemptIndex => {
            // attemptIndex: 0 = first retry, 1 = second retry, etc.
            // Fast retries first, then slow down
            return attemptIndex switch {
                0 => TimeSpan.FromMilliseconds(100),  // 100ms
                1 => TimeSpan.FromMilliseconds(500),  // 500ms
                _ => TimeSpan.FromSeconds(5)          // 5s for remaining
            };
        }
    ),
    queryClient
);

// Fixed delay (no backoff)
var query = new UseQuery<string>(
    new QueryOptions<string>(
        queryKey: new("data"),
        queryFn: async ctx => await FetchDataAsync(),
        retry: 3,
        retryDelay: TimeSpan.FromSeconds(1)  // Always wait 1 second
    ),
    queryClient
);
```

# Pause and Continue on Network Changes

When using `NetworkMode.Online` or `NetworkMode.OfflineFirst`, retries pause when going offline:

| Scenario | Behavior |
|----------|----------|
| **During active fetch** | Fetch is cancelled, query enters `Paused` state |
| **During retry delay** | Delay waits for network to return |
| **When back online** | Query **continues** from where it left off (same attempt count) |
| **If disposed while paused** | Query will not continue |

> **Important**: This is a **continue** operation, not a refetch. The attempt count is preserved.

Queries with `NetworkMode.Always` do not pause — they fail immediately if the network is unavailable.

# Failure Tracking

| Property | Description |
|----------|-------------|
| `FailureCount` | Number of failed attempts so far (increments with each failure) |
| `FailureReason` | Exception from the most recent failed attempt |
| `Error` | Final error after all retries exhausted |

```csharp
// Monitor retry progress
query.OnChange += () => {
    if (query.FailureCount > 0 && query.Error == null)
    {
        // Still retrying
        Console.WriteLine($"Attempt {query.FailureCount} failed: {query.FailureReason?.Message}");
        Console.WriteLine($"Retrying...");
    }
    else if (query.Error != null)
    {
        // All retries exhausted
        Console.WriteLine($"Failed after {query.FailureCount} attempts: {query.Error.Message}");
    }
};
```

# Not Yet Implemented

> **`refetchIntervalInBackground`**: React Query pauses interval refetches when the browser tab 
> is inactive. This is browser-specific and not implemented in SwrSharp.
>
> For Blazor WebAssembly, you can implement this manually using the Page Visibility API via JS interop:
>
> ```csharp
> @inject IJSRuntime JS
>
> @code {
>     private bool _isVisible = true;
>
>     protected override async Task OnAfterRenderAsync(bool firstRender)
>     {
>         if (firstRender)
>         {
>             await JS.InvokeVoidAsync("setupVisibilityListener",
>                 DotNetObjectReference.Create(this));
>         }
>     }
>
>     [JSInvokable]
>     public void OnVisibilityChange(bool isVisible)
>     {
>         _isVisible = isVisible;
>         // Pause/resume refetch based on visibility
>     }
> }
> ```
