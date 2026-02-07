---
title: "UseQuery"
description: "API reference for the UseQuery hook"
order: 1
category: "API"
---

# UseQuery

The `UseQuery` hook is the primary way to fetch and cache data in SwrSharp.

## Usage

```csharp
var query = await QueryClient.UseQuery(
    queryKey: new QueryKey("todos"),
    queryFn: async ctx => await FetchTodos(ctx.Signal),
    options: new QueryOptions { /* ... */ }
);
```

## Parameters

### `queryKey` (QueryKey)
A unique identifier for the query. Used for caching and invalidation.

```csharp
// Simple key
new QueryKey("todos")

// With parameters
new QueryKey("todos", userId)

// Multiple parameters
new QueryKey("user", userId, "posts")
```

### `queryFn` (Func<QueryFunctionContext, Task<T>>)
The function that fetches the data.

```csharp
async ctx => {
    var (key, signal) = ctx;
    return await Http.GetFromJsonAsync<Todo[]>("/api/todos", signal);
}
```

### `options` (QueryOptions, optional)
Configuration options for the query.

## Return Value

Returns `UseQueryResult<T>` with the following properties:

```csharp
public class UseQueryResult<T>
{
    public T? Data { get; }                           // The fetched data
    public Exception? Error { get; }                  // Error if query failed
    public QueryStatus Status { get; }                // Pending | Success | Error
    public FetchStatus FetchStatus { get; }           // Idle | Fetching | Paused
    
    public bool IsLoading { get; }                    // Status == Pending && IsFetching
    public bool IsPending { get; }                    // Status == Pending
    public bool IsSuccess { get; }                    // Status == Success
    public bool IsError { get; }                      // Status == Error
    public bool IsFetching { get; }                   // FetchStatus == Fetching
    public bool IsPaused { get; }                     // FetchStatus == Paused
    
    public int FailureCount { get; }                  // Number of failures
    public Exception? FailureReason { get; }          // Last failure reason
    
    public DateTime? DataUpdatedAt { get; }           // When data was last updated
    public DateTime? ErrorUpdatedAt { get; }          // When error was last updated
}
```

## Example

```csharp
@page "/todos"
@inject QueryClient QueryClient
@implements IAsyncDisposable

<div>
    @if (Query.IsLoading)
    {
        <p>Loading...</p>
    }
    else if (Query.IsError)
    {
        <p>Error: @Query.Error?.Message</p>
    }
    else if (Query.Data != null)
    {
        @foreach (var todo in Query.Data)
        {
            <div class="todo">@todo.Title</div>
        }
    }
</div>

@code {
    private UseQueryResult<Todo[]>? Query;

    protected override async Task OnInitializedAsync()
    {
        Query = await QueryClient.UseQuery(
            queryKey: new QueryKey("todos"),
            queryFn: FetchTodos
        );
    }

    private async Task<Todo[]> FetchTodos(QueryFunctionContext ctx)
    {
        return await Http.GetFromJsonAsync<Todo[]>("/api/todos", ctx.Signal) 
            ?? Array.Empty<Todo>();
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        if (Query != null)
            await Query.DisposeAsync();
    }
}
```

## See Also

- [UseInfiniteQuery](/docs/api/use-infinite-query)
- [QueryClient](/docs/api/query-client)
- [Query Options](/docs/guides/query-options)

