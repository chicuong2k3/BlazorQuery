---
title: "Query Retries"
description: "Configuring retry behavior"
order: 4
category: "Guides"
---



# Query Retries in SwrSharp

When a query fails (the query function throws an exception), **SwrSharp** will automatically retry 
the query if the number of consecutive retries has not exceeded the limit specified 
in the query options.

You can configure retries both on a global level (via `QueryClient` defaults) and 
on an individual query level (`QueryOptions<T>`).

### Retry Options

- Setting `Retry = 0` will disable retries.
- Setting `Retry = 6` will retry failing requests **6 times** before showing the final error (7 total attempts).
- Setting `RetryInfinite = true` will infinitely retry failing requests.
- Custom retry logic can be provided via `RetryFunc: Func<int, Exception, bool>`, 
allowing conditional retries depending on the attempt number or exception.
- Retries apply only when an exception is thrown from the query function.

**Note**: This matches React Query behavior exactly. `retry: 6` means 6 retries **after** 
the initial attempt, for a total of 7 attempts.

```csharp
var query = new UseQuery<string>(
    new QueryOptions<string>(
        queryKey: new("todos", 1),
        queryFn: async ctx => await FetchTodoListPageAsync(),
        staleTime: TimeSpan.FromMinutes(5),
        networkMode: NetworkMode.Online,
        refetchOnReconnect: true,
        retry: 10 // Will retry failed requests 10 times (11 total attempts)
    ),
    queryClient
);
```

### FailureReason Property

During retry attempts, the error from each failed attempt is available via the `FailureReason` property.
After the last retry attempt fails, this error becomes the `Error` property.

```csharp
// During retries
if (query.FailureReason != null)
{
    Console.WriteLine($"Retry attempt failed: {query.FailureReason.Message}");
}

// After all retries exhausted
if (query.Error != null)
{
    Console.WriteLine($"Final error: {query.Error.Message}");
}
```

# Retry Delay

Retries in SwrSharp are not immediate. A backoff delay is applied between retry 
attempts to reduce load and collisions.

By default, **SwrSharp uses exponential backoff** matching React Query:
- Starts at **1000ms** for the first retry
- Doubles with each retry: 1000ms → 2000ms → 4000ms → 8000ms...
- Maximum delay is capped by `MaxRetryDelay` (default: 30 seconds)
- Formula: `Math.Min(1000 * 2^attemptIndex, 30000)`
- Optionally, a custom retry delay function can be provided via 
`RetryDelayFunc: Func<int, TimeSpan>` where `attemptIndex` starts at 0 for the first retry.

```csharp
int delayMs;
if (_queryOptions.RetryDelayFunc != null)
{
    delayMs = (int)_queryOptions.RetryDelayFunc(attemptIndex).TotalMilliseconds;
}
else
{
    // Default: Math.min(1000 * 2^attemptIndex, 30000)
    double expDelay = 1000 * Math.Pow(2, attemptIndex);
    delayMs = (int)Math.Min(expDelay, maxRetryDelay.TotalMilliseconds);
}

await Task.Delay(delayMs, cancellationToken);
```

# Custom Retry Delay

- Provide a custom delay function via `RetryDelayFunc: Func<int, TimeSpan>`.
- The function receives `attemptIndex` (0-based: 0 = first retry, 1 = second retry, etc.) 
and returns a `TimeSpan` to wait before the next retry.

```csharp
var query = new UseQuery<string>(
    new QueryOptions<string>(
        queryKey: new("todos"),
        queryFn: async ctx => await FetchTodosAsync(),
        retry: 5,
        retryDelayFunc: (attemptIndex) => {
            // Custom logic: shorter delay for first few attempts
            if (attemptIndex < 2)
                return TimeSpan.FromMilliseconds(500);
            return TimeSpan.FromSeconds(5);
        }
    ),
    queryClient
);
```

Or set a constant delay:

```csharp
var query = new UseQuery<string>(
    new QueryOptions<string>(
        queryKey: new("todos"),
        queryFn: async ctx => await FetchTodosAsync(),
        retryDelay: TimeSpan.FromSeconds(1) // Always wait 1 second
    ),
    queryClient
);
```

# Pause and Continue on Network Changes

If a query is running and you go offline while the fetch is still in progress,
SwrSharp will pause the retry mechanism:

- **During active fetch**: The current fetch is cancelled and the query enters `Paused` state.
- **During retry delay**: The retry waits for the network to return before continuing.
- **Resume behavior**: Once back online, the query **continues** from where it left off
  (same attempt count) — this is NOT a refetch.
- **Cancellation**: If the query was cancelled while paused (e.g., component disposed),
  it will not continue when the network returns.

This behavior only applies to `NetworkMode.Online` and `NetworkMode.OfflineFirst`.
Queries with `NetworkMode.Always` do not pause and will fail immediately if the network is unavailable.

# Failure Tracking

SwrSharp provides properties to track retry failures:

- **`FailureCount`**: The number of failed attempts so far (increments with each retry failure).
- **`FailureReason`**: The exception from the most recent retry attempt. This is available during
  retry attempts before the final `Error` is set. After the last retry fails, this becomes the `Error`.

```csharp
// During retries, FailureReason contains the current error
if (query.FailureCount > 0 && query.FailureReason != null)
{
    Console.WriteLine($"Attempt {query.FailureCount} failed: {query.FailureReason.Message}");
}

// After all retries exhausted, Error is set
if (query.Error != null)
{
    Console.WriteLine($"Query failed after {query.FailureCount} attempts: {query.Error.Message}");
}
```

# Background Retry Behavior

> **Not yet implemented**: TanStack Query supports `refetchIntervalInBackground` which pauses
> interval refetches when the browser tab is inactive. This is a browser-specific feature.
>
> In Blazor Server, the connection remains active regardless of tab visibility.
> In Blazor WebAssembly, you could implement this using the Page Visibility API via JS interop
> and conditionally pause/resume the query's refetch interval.
>
> If you need this feature, consider implementing a custom solution:
>
> ```csharp
> // Example: Manual control of refetch interval based on visibility
> @inject IJSRuntime JS
>
> @code {
>     private bool _isVisible = true;
>
>     protected override async Task OnAfterRenderAsync(bool firstRender)
>     {
>         if (firstRender)
>         {
>             // Set up visibility change listener via JS interop
>             await JS.InvokeVoidAsync("setupVisibilityListener",
>                 DotNetObjectReference.Create(this));
>         }
>     }
>
>     [JSInvokable]
>     public void OnVisibilityChange(bool isVisible)
>     {
>         _isVisible = isVisible;
>         // Manually pause/resume refetch or dispose/recreate query
>     }
> }
> ```
