---
title: "QueryClient"
description: "QueryClient API reference"
order: 3
category: "API"
---


# QueryClient

The `QueryClient` is the core service that manages all queries, caching, and data synchronization.

## Dependency Injection

Inject `QueryClient` into your components:

```csharp
@inject QueryClient QueryClient
```

## Methods

### UseQuery

Fetch and cache data:

```csharp
var result = await QueryClient.UseQuery(
    queryKey: new QueryKey("todos"),
    queryFn: FetchTodos,
    options: new QueryOptions { }
);
```

See [UseQuery API](/docs/api/use-query) for details.

### UseInfiniteQuery

Fetch paginated or infinite data:

```csharp
var result = await QueryClient.UseInfiniteQuery(
    queryKey: new QueryKey("posts"),
    queryFn: FetchPostsPage,
    options: new InfiniteQueryOptions { }
);
```

See [UseInfiniteQuery API](/docs/api/use-infinite-query) for details.

### InvalidateQueries

Mark queries as stale to trigger refetch:

```csharp
// Invalidate specific query
await QueryClient.InvalidateQueries(new QueryKey("todos"));

// Invalidate queries matching a filter
await QueryClient.InvalidateQueries(
    filter: new QueryFilters 
    { 
        QueryKey = new QueryKey("todos") 
    }
);
```

### GetQueryData

Get cached data without triggering fetch:

```csharp
var todos = QueryClient.GetQueryData<Todo[]>(new QueryKey("todos"));
```

### SetQueryData

Manually set cached data:

```csharp
QueryClient.SetQueryData(
    new QueryKey("todos"), 
    new[] { new Todo { Id = 1, Title = "New Todo" } }
);
```

### CancelQueries

Cancel all fetching queries:

```csharp
await QueryClient.CancelQueries();

// Cancel specific queries
await QueryClient.CancelQueries(
    filter: new QueryFilters 
    { 
        QueryKey = new QueryKey("todos") 
    }
);
```

### RemoveQueries

Remove queries from cache:

```csharp
await QueryClient.RemoveQueries(new QueryKey("todos"));
```

### ClearCache

Clear all cached data:

```csharp
await QueryClient.ClearCache();
```

## Properties

### OnlineManager
Access the online status manager:

```csharp
bool isOnline = QueryClient.OnlineManager.IsOnline;
QueryClient.OnlineManager.SetOnline(false);
```

### FocusManager
Access the window focus manager:

```csharp
// Refetch all stale queries when window focuses
QueryClient.FocusManager.OnWindowFocus();
```

## Example Usage

```csharp
@page "/todos"
@inject QueryClient QueryClient
@inject HttpClient Http
@implements IAsyncDisposable

<h1>Todos</h1>

@if (TodosQuery?.IsLoading ?? false)
{
    <p>Loading...</p>
}
else if (TodosQuery?.IsError ?? false)
{
    <p>Error: @TodosQuery.Error?.Message</p>
}
else if (TodosQuery?.Data != null)
{
    <ul>
        @foreach (var todo in TodosQuery.Data)
        {
            <li>@todo.Title</li>
        }
    </ul>
}

<button @onclick="RefreshTodos">Refresh</button>
<button @onclick="ClearCache">Clear Cache</button>

@code {
    private UseQueryResult<Todo[]>? TodosQuery;

    protected override async Task OnInitializedAsync()
    {
        TodosQuery = await QueryClient.UseQuery(
            queryKey: new QueryKey("todos"),
            queryFn: FetchTodos,
            options: new QueryOptions 
            { 
                StaleTime = TimeSpan.FromMinutes(5) 
            }
        );
    }

    private async Task FetchTodos(QueryFunctionContext ctx)
    {
        return await Http.GetFromJsonAsync<Todo[]>("/api/todos", ctx.Signal) 
            ?? Array.Empty<Todo>();
    }

    private async Task RefreshTodos()
    {
        await QueryClient.InvalidateQueries(new QueryKey("todos"));
    }

    private async Task ClearCache()
    {
        await QueryClient.ClearCache();
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        if (TodosQuery != null)
            await TodosQuery.DisposeAsync();
    }
}
```

## See Also

- [Query Invalidation](/docs/guides/query-invalidation)
- [Query Cancellation](/docs/guides/query-cancellation)
- [Using Filters](/docs/guides/filters)
