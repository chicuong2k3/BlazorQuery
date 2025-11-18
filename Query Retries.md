

# Query Retries in BlazorQuery

When a query fails (the query function throws an exception), BlazorQuery will automatically retry the query if the number of consecutive retries has not exceeded 
the limit specified in the query options.

You can configure retries both on a global level (via `QueryClient` defaults) and on an individual query level (`QueryOptions<T>`).

### Retry Options

- Setting `Retry = 0` will disable retries.
- Setting `Retry = 3` will retry failing requests 3 times before throwing the final exception.
- **Note:** Infinite retries (`Retry = true`) and custom retry logic (`Func<int, Exception, bool>`) are not currently supported.
- Retries apply only when an exception is thrown from the query function.

```csharp
var queryOptions = new QueryOptions<MyData>(
    queryKey: new QueryKey("todos", 1),
    queryFn: async ctx => await FetchTodoListPageAsync(),
    staleTime: TimeSpan.FromMinutes(5),
    networkMode: NetworkMode.Online,
    refetchOnReconnect: true,
    retry: 10 // Will retry failed requests 10 times before throwing an exception
);

var resultQuery = new UseQuery<MyData>(queryOptions, queryClient);
```

Retry Delay

By default, retries in BlazorQuery use an exponential backoff, starting at 1000ms and doubling each attempt:

await Task.Delay((int)Math.Pow(2, attempt) * 1000, cancellationToken);


The delay increases with each retry attempt.

The maximum delay is not capped by default.

Currently, there is no option to override the retry delay with a fixed value or custom function, unlike React Query.

// Example: retry delay is calculated as 2^attempt * 1000ms
for (int attempt = 0; attempt <= maxRetries; attempt++)
{
    try
    {
        Data = await _client.FetchAsync(...);
        break;
    }
    catch (Exception ex)
    {
        FailureCount++;
        if (attempt < maxRetries)
        {
            await Task.Delay((int)Math.Pow(2, attempt) * 1000, token);
        }
    }
}

BlazorQuery currently does not support infinite retries, custom retry logic, or configurable retry delays.