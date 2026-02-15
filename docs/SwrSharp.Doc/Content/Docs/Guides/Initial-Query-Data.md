---
title: "Initial Query Data"
description: "Providing initial data"
order: 11
category: "Guides"
---

# Initial Query Data

There are many ways to supply initial data for a query to the cache before you need it:

- **Declaratively**:
  - Provide `initialData` to a query to prepopulate its cache if empty
- **Imperatively**:
  - Prefetch the data using `queryClient.PrefetchQuery` (covered in Prefetching guide)
  - Manually place the data into the cache using `queryClient.SetQueryData`

## Using `initialData` to Prepopulate a Query

There may be times when you already have the initial data for a query available in your app and can simply provide it directly to your query. If and when this is the case, you can use the `initialData` option to set the initial data for a query and skip the initial loading state!

> **IMPORTANT**: `initialData` is persisted to the cache, so it is not recommended to provide placeholder, partial or incomplete data to this option. For placeholder data, use `placeholderData` instead (covered in a separate guide).

```csharp
var initialTodos = new List<Todo>
{
    new() { Id = 1, Title = "Learn SwrSharp" },
    new() { Id = 2, Title = "Build awesome app" }
};

var query = new UseQuery<List<Todo>>(
    new QueryOptions<List<Todo>>(
        queryKey: new("todos"),
        queryFn: async ctx => await FetchTodosAsync(),
        initialData: initialTodos
    ),
    queryClient
);

// Query immediately has data, no loading state
Console.WriteLine($"Status: {query.Status}"); // Success
Console.WriteLine($"Data count: {query.Data!.Count}"); // 2
```

## `staleTime` and `initialDataUpdatedAt`

By default, `initialData` is treated as totally fresh, as if it were just fetched. This also means that it will affect how it is interpreted by the `staleTime` option.

### Without `staleTime` (Immediate Refetch)

If you configure your query with `initialData`, and no `staleTime` (the default `staleTime: TimeSpan.Zero`), the query will immediately refetch when you call `ExecuteAsync`:

```csharp
// Will show initialTodos immediately, but also immediately refetch after ExecuteAsync
var query = new UseQuery<List<Todo>>(
    new QueryOptions<List<Todo>>(
        queryKey: new("todos"),
        queryFn: async ctx => await FetchTodosAsync(),
        initialData: initialTodos
        // staleTime defaults to TimeSpan.Zero
    ),
    queryClient
);

// Has initial data immediately (before any fetch)
Console.WriteLine(query.Data!.Count); // 2 (initial data)

query.OnChange += () =>
{
    // Called when refetch completes with fresh server data
    if (query.IsSuccess && query.Data != null)
    {
        Console.WriteLine($"Updated: {query.Data.Count} todos from server");
        // Notify your UI framework to re-render
    }
};

// Will refetch because staleTime = 0 (immediately stale)
_ = query.ExecuteAsync();
```

### With `staleTime` (Delayed Refetch)

If you configure your query with `initialData` and a `staleTime`, the data will be considered fresh for that same amount of time, as if it was just fetched from your query function.

```csharp
// Show initialTodos immediately, won't refetch for 1 second
var query = new UseQuery<List<Todo>>(
    new QueryOptions<List<Todo>>(
        queryKey: new("todos"),
        queryFn: async ctx => await FetchTodosAsync(),
        initialData: initialTodos,
        staleTime: TimeSpan.FromSeconds(1)
    ),
    queryClient
);

query.OnChange += () =>
{
    if (query.IsSuccess && query.Data != null)
    {
        Console.WriteLine($"Data updated: {query.Data.Count} todos");
        // Notify your UI framework to re-render
    }
};

// Has data, considered fresh — ExecuteAsync won't actually fetch
_ = query.ExecuteAsync();

// After 1 second, data becomes stale.
// A subsequent ExecuteAsync (e.g., triggered by window focus or manual refetch)
// will refetch from the server.
```

### With `initialDataUpdatedAt` (Accurate Staleness)

So what if your `initialData` isn't totally fresh? That leaves us with the most accurate configuration that uses `initialDataUpdatedAt`. This option allows you to pass a `DateTime` of when the initialData itself was last updated.

```csharp
// Initial data from local storage (might be old)
var cachedTodos = LoadTodosFromLocalStorage();
var cachedTimestamp = LoadTimestampFromLocalStorage(); // DateTime

var query = new UseQuery<List<Todo>>(
    new QueryOptions<List<Todo>>(
        queryKey: new("todos"),
        queryFn: async ctx => await FetchTodosAsync(),
        initialData: cachedTodos,
        staleTime: TimeSpan.FromMinutes(1), // Fresh for 1 minute
        initialDataUpdatedAt: cachedTimestamp // When was it cached?
    ),
    queryClient
);

query.OnChange += () =>
{
    if (query.IsSuccess && query.Data != null)
    {
        // Notify your UI framework to re-render
    }
};

// If cachedTimestamp is < 1 minute old: won't refetch (still fresh)
// If cachedTimestamp is > 1 minute old: will refetch (stale)
_ = query.ExecuteAsync();
```

This option allows the `staleTime` to be used for its original purpose, determining how fresh the data needs to be, while also allowing the data to be refetched if the `initialData` is older than the `staleTime`.

## Initial Data Function

If the process for accessing a query's initial data is intensive or just not something you want to perform on every render, you can pass a function as the `initialDataFunc` value. This function will be executed only once when the query is initialized, saving you precious memory and/or CPU:

```csharp
var query = new UseQuery<List<Todo>>(
    new QueryOptions<List<Todo>>(
        queryKey: new("todos"),
        queryFn: async ctx => await FetchTodosAsync(),
        initialDataFunc: () => {
            // Expensive operation - only called once
            Console.WriteLine("Computing expensive initial data...");
            return GetExpensiveTodos();
        }
    ),
    queryClient
);

// Expensive function called only once during initialization
```

## Initial Data from Cache

In some circumstances, you may be able to provide the initial data for a query from the cached result of another query. A good example of this would be searching the cached data from a todos list query for an individual todo item, then using that as the initial data for your individual todo query:

```csharp
// Assume a todos list query has already been executed and cached.
// When navigating to a todo detail, use cached data as initial data:

var todoId = 123;
var todoQuery = new UseQuery<Todo>(
    new QueryOptions<Todo>(
        queryKey: new("todo", todoId),
        queryFn: async ctx => await FetchTodoAsync(todoId),
        initialDataFunc: () => {
            // Use a todo from the 'todos' query as initial data
            var todos = queryClient.GetQueryData<List<Todo>>(new("todos"));
            return todos?.Find(t => t.Id == todoId);
        }
    ),
    queryClient
);

// todoQuery immediately has data from cache (if found)
if (todoQuery.Data != null)
{
    Console.WriteLine($"Found in cache: {todoQuery.Data.Title}");
}

todoQuery.OnChange += () =>
{
    if (todoQuery.IsSuccess && todoQuery.Data != null)
    {
        // Notify your UI framework to re-render
    }
};

// Will refetch from server (staleTime defaults to 0)
_ = todoQuery.ExecuteAsync();
```

## Initial Data from Cache with `initialDataUpdatedAt`

Getting initial data from the cache means the source query you're using to look up the initial data from is likely old. Instead of using an artificial `staleTime` to keep your query from refetching immediately, it's suggested that you pass the source query's `dataUpdatedAt` to `initialDataUpdatedAt`. This provides the query instance with all the information it needs to determine if and when the query needs to be refetched, regardless of initial data being provided.

```csharp
var todoId = 123;
var todoQuery = new UseQuery<Todo>(
    new QueryOptions<Todo>(
        queryKey: new("todo", todoId),
        queryFn: async ctx => await FetchTodoAsync(todoId),
        initialDataFunc: () => {
            var todos = queryClient.GetQueryData<List<Todo>>(new("todos"));
            return todos?.Find(t => t.Id == todoId);
        },
        // Pass the source query's dataUpdatedAt timestamp
        initialDataUpdatedAt: queryClient.GetQueryState(new("todos"))?.DataUpdatedAt,
        staleTime: TimeSpan.FromMinutes(5)
    ),
    queryClient
);

// If source todos query is < 5 minutes old: won't refetch
// If source todos query is > 5 minutes old: will refetch
```

## Conditional Initial Data from Cache

If the source query you're using to look up the initial data from is old, you may not want to use the cached data at all and just fetch from the server. To make this decision easier, you can use the `queryClient.GetQueryState` method to get more information about the source query, including a `DataUpdatedAt` timestamp you can use to decide if the query is "fresh" enough for your needs:

```csharp
var todoId = 123;
var todoQuery = new UseQuery<Todo>(
    new QueryOptions<Todo>(
        queryKey: new("todo", todoId),
        queryFn: async ctx => await FetchTodoAsync(todoId),
        initialDataFunc: () => {
            // Get the query state
            var state = queryClient.GetQueryState(new("todos"));
            
            // If the query exists and has data that is no older than 10 seconds...
            if (state != null && (DateTime.UtcNow - state.DataUpdatedAt).TotalSeconds <= 10)
            {
                // Return the individual todo
                var todos = state.Data as List<Todo>;
                return todos?.Find(t => t.Id == todoId);
            }
            
            // Otherwise, return null and let it fetch from a hard loading state!
            return null;
        }
    ),
    queryClient
);
```

## Complete Example: Todo List and Detail

```csharp
public class TodoApp : IDisposable
{
    private readonly QueryClient _queryClient;
    private UseQuery<List<Todo>>? _todosQuery;
    private UseQuery<Todo>? _todoDetailQuery;

    public TodoApp()
    {
        _queryClient = new QueryClient();
    }

    public void LoadTodosList()
    {
        _todosQuery?.Dispose();

        _todosQuery = new UseQuery<List<Todo>>(
            new QueryOptions<List<Todo>>(
                queryKey: new("todos"),
                queryFn: async ctx => await FetchTodosAsync(),
                staleTime: TimeSpan.FromMinutes(5)
            ),
            _queryClient
        );

        _todosQuery.OnChange += () =>
        {
            if (_todosQuery.IsSuccess && _todosQuery.Data != null)
            {
                // "Loaded {count} todos"
                // Notify your UI framework to re-render
            }
        };

        _ = _todosQuery.ExecuteAsync();
    }

    public void LoadTodoDetail(int todoId)
    {
        _todoDetailQuery?.Dispose();

        _todoDetailQuery = new UseQuery<Todo>(
            new QueryOptions<Todo>(
                queryKey: new("todo", todoId),
                queryFn: async ctx => await FetchTodoAsync(todoId),
                initialDataFunc: () =>
                {
                    var state = _queryClient.GetQueryState(new("todos"));

                    // Only use cached data if it's fresh (< 5 minutes old)
                    if (state != null &&
                        (DateTime.UtcNow - state.DataUpdatedAt).TotalSeconds <= 300)
                    {
                        var todos = state.Data as List<Todo>;
                        return todos?.Find(t => t.Id == todoId);
                    }

                    return null; // Let it fetch from server
                },
                initialDataUpdatedAt: _queryClient.GetQueryState(new("todos"))?.DataUpdatedAt,
                staleTime: TimeSpan.FromMinutes(5)
            ),
            _queryClient
        );

        // If found in cache and fresh: shows immediately
        if (_todoDetailQuery.Data != null)
        {
            // "Instant: {title}" — display cached data right away
        }

        _todoDetailQuery.OnChange += () =>
        {
            if (_todoDetailQuery.IsSuccess && _todoDetailQuery.Data != null)
            {
                // "Todo: {title}" — updated from server (or confirmed fresh)
                // Notify your UI framework to re-render
            }
        };

        // If initial data is stale or missing: fetches from server
        _ = _todoDetailQuery.ExecuteAsync();
    }

    public void Dispose()
    {
        _todosQuery?.Dispose();
        _todoDetailQuery?.Dispose();
        _queryClient.Dispose();
    }
}
```