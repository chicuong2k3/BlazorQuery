---
title: "Query Invalidation"
description: "Invalidating queries"
order: 15
category: "Guides"
---

Waiting for queries to become stale before they are fetched again doesn't always work, especially when you know for a fact that a query's data is out of date because of something the user has done. For that purpose, the `QueryClient` has an `InvalidateQueries` method that lets you intelligently mark queries as stale and potentially refetch them too!

```csharp
// Invalidate every query in the cache
queryClient.InvalidateQueries();

// Invalidate every query with a key that starts with "todos"
queryClient.InvalidateQueries(new QueryFilters 
{ 
    QueryKey = new("todos") 
});
```

> **Note**: Where other libraries that use normalized caches would attempt to update local queries with the new data either imperatively or via schema inference, SwrSharp gives you the tools to avoid the manual labor that comes with maintaining normalized caches and instead prescribes **targeted invalidation, background-refetching and ultimately atomic updates**.

## What Happens When a Query is Invalidated?

When a query is invalidated with `InvalidateQueries`, two things happen:

1. **It is marked as stale**. This stale state overrides any `staleTime` configurations being used in `UseQuery`
2. **If the query is currently being rendered** (has active `UseQuery` instances), it will also be refetched in the background automatically

## Query Matching with `InvalidateQueries`

When using `InvalidateQueries`, you can match multiple queries by their prefix, get really specific and match an exact query, or use custom predicates.

### Prefix Matching (Default)

In this example, we can use the "todos" prefix to invalidate any queries that start with "todos" in their query key:

```csharp
var queryClient = new QueryClient();

// Invalidate with prefix
queryClient.InvalidateQueries(new QueryFilters 
{ 
    QueryKey = new("todos") 
});

// Both queries below will be invalidated
var todoListQuery = new UseQuery<List<Todo>>(
    new QueryOptions<List<Todo>>(
        queryKey: new("todos"),
        queryFn: async ctx => await FetchTodoListAsync()
    ),
    queryClient
);

var todoDetailQuery = new UseQuery<Todo>(
    new QueryOptions<Todo>(
        queryKey: new("todos", new { page = 1 }),
        queryFn: async ctx => await FetchTodoListAsync(page: 1)
    ),
    queryClient
);
```

### Specific Variables

You can even invalidate queries with specific variables by passing a more specific query key:

```csharp
queryClient.InvalidateQueries(new QueryFilters
{
    QueryKey = new("todos", new { type = "done" })
});

// The query below will be invalidated
var query1 = new UseQuery<List<Todo>>(
    new QueryOptions<List<Todo>>(
        queryKey: new("todos", new { type = "done" }),
        queryFn: async ctx => await FetchTodoListAsync()
    ),
    queryClient
);

// However, the following query below will NOT be invalidated
var query2 = new UseQuery<List<Todo>>(
    new QueryOptions<List<Todo>>(
        queryKey: new("todos"),
        queryFn: async ctx => await FetchTodoListAsync()
    ),
    queryClient
);
```

### Exact Matching

The `InvalidateQueries` API is very flexible, so even if you want to **only** invalidate "todos" queries that don't have any more variables or subkeys, you can pass `Exact = true` option:

```csharp
queryClient.InvalidateQueries(new QueryFilters
{
    QueryKey = new("todos"),
    Exact = true
});

// The query below will be invalidated
var query1 = new UseQuery<List<Todo>>(
    new QueryOptions<List<Todo>>(
        queryKey: new("todos"),
        queryFn: async ctx => await FetchTodoListAsync()
    ),
    queryClient
);

// However, the following query below will NOT be invalidated
var query2 = new UseQuery<List<Todo>>(
    new QueryOptions<List<Todo>>(
        queryKey: new("todos", new { type = "done" }),
        queryFn: async ctx => await FetchTodoListAsync()
    ),
    queryClient
);
```

### Custom Predicate

If you find yourself wanting **even more** granularity, you can pass a predicate function. This function will receive each `QueryKey` from the query cache and allow you to return `true` or `false` for whether you want to invalidate that query:

```csharp
queryClient.InvalidateQueries(new QueryFilters
{
    Predicate = key => {
        if (key.Parts.Count < 2 || key.Parts[0]?.ToString() != "todos")
            return false;
        
        // Get version from anonymous object in key
        var versionObj = key.Parts[1];
        var versionProp = versionObj?.GetType().GetProperty("version");
        var version = (int?)versionProp?.GetValue(versionObj);
        
        return version >= 10;
    }
});

// The query below will be invalidated
var query1 = new UseQuery<List<Todo>>(
    new QueryOptions<List<Todo>>(
        queryKey: new("todos", new { version = 20 }),
        queryFn: async ctx => await FetchTodoListAsync()
    ),
    queryClient
);

// The query below will be invalidated
var query2 = new UseQuery<List<Todo>>(
    new QueryOptions<List<Todo>>(
        queryKey: new("todos", new { version = 10 }),
        queryFn: async ctx => await FetchTodoListAsync()
    ),
    queryClient
);

// However, the following query below will NOT be invalidated
var query3 = new UseQuery<List<Todo>>(
    new QueryOptions<List<Todo>>(
        queryKey: new("todos", new { version = 5 }),
        queryFn: async ctx => await FetchTodoListAsync()
    ),
    queryClient
);
```

## Common Use Cases

### After Mutations

Use `UseMutation`'s lifecycle callbacks to invalidate queries after creating, updating, or deleting data:

```csharp
var createTodo = new UseMutation<Todo, CreateTodoInput>(
    new MutationOptions<Todo, CreateTodoInput>
    {
        MutationFn = async input => await PostTodo(input),
        OnSuccess = async (data, variables, onMutateResult, context) =>
        {
            // Invalidate todos list to refetch with new item
            context.Client.InvalidateQueries(new QueryFilters
            {
                QueryKey = new QueryKey("todos")
            });
        }
    },
    queryClient
);

var updateTodo = new UseMutation<Todo, UpdateTodoInput>(
    new MutationOptions<Todo, UpdateTodoInput>
    {
        MutationFn = async input => await UpdateTodo(input),
        OnSuccess = async (data, variables, onMutateResult, context) =>
        {
            // Invalidate both list and detail
            context.Client.InvalidateQueries(new QueryFilters
            {
                QueryKey = new QueryKey("todos")
            });
        }
    },
    queryClient
);
```

See [Invalidations from Mutations](/docs/Guides/Invalidations-from-Mutations) for more patterns.

### User Actions

Invalidate when user explicitly requests fresh data:

```csharp
void OnRefreshButtonClick()
{
    // User clicked refresh - invalidate current page data
    _queryClient.InvalidateQueries(new QueryFilters
    {
        QueryKey = new("currentPage")
    });
    // Active queries with this key will automatically refetch via OnChange
}
```

### Background Sync

Invalidate after background sync completes. Use `OnChange` to react to the refetch results:

```csharp
public class SyncService
{
    private readonly QueryClient _queryClient;

    public void PerformBackgroundSync()
    {
        // Sync data with server, then invalidate
        _ = SyncAndInvalidateAsync();
    }

    private async Task SyncAndInvalidateAsync()
    {
        await SyncWithServerAsync();

        // Invalidate all queries to show fresh data
        // Active queries will refetch and notify via OnChange
        _queryClient.InvalidateQueries();
    }
}
```