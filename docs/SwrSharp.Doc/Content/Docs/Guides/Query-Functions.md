---
title: "Query Functions"
description: "Writing query functions"
order: 2
category: "Guides"
---


# What is a Query Function?

A **Query Function** is any asynchronous method that fetches data. 
It must either return **a result** or **throw an exception** so that SwrSharp can track **loading** 
and **error** states.

Query functions receive a `QueryFunctionContext` that provides:
- `QueryKey`: uniquely identifies the query.
- `Signal`: a `CancellationToken` that allows query cancellation.
- `Meta`: optional metadata for the query.
- `PageParam`: (for infinite queries only) the parameter for fetching the current page.
- `Direction`: (for infinite queries only) `Forward` or `Backward` indicating fetch direction.
- `Client`: access to the `QueryClient` instance for cache operations.

# Basic Usage

You can define a query function in several ways depending on your needs, including inline 
lambdas or extracted methods:

```csharp
// Simple query — fetches a fixed resource (no dynamic parameters)
var query = new UseQuery<List<string>>(
    new QueryOptions<List<string>>(
        queryKey: new("todos"),
        queryFn: async ctx => await FakeApi.GetTodosAsync()
    ),
    _queryClient
);

// Query with parameter — fetches different data based on todoId
// The parameter is captured from outer scope (closure)
var todoId = 5;
var query = new UseQuery<string>(
    new QueryOptions<string>(
        queryKey: new("todo", todoId),  // todoId is part of the key
        queryFn: async ctx => await FakeApi.GetTodoByIdAsync(todoId)  // todoId captured here
    ),
    _queryClient
);

// Better approach: extract parameter from QueryKey (no closure needed)
var query = new UseQuery<string>(
    new QueryOptions<string>(
        queryKey: new("todo", todoId),
        queryFn: async ctx => {
            var id = (int)ctx.QueryKey[1]!;  // Get todoId from query key
            return await FakeApi.GetTodoByIdAsync(id);
        }
    ),
    _queryClient
);

// Cleanest: use destructuring
var query = new UseQuery<string>(
    new QueryOptions<string>(
        queryKey: new("todo", todoId),
        queryFn: async ctx => {
            var (queryKey, signal) = ctx;
            var id = (int)queryKey[1]!;
            return await FakeApi.GetTodoByIdAsync(id);
        }
    ),
    _queryClient
);
```

> **Why extract from QueryKey?** Using `ctx.QueryKey` instead of closure variables makes 
> your query function reusable and ensures consistency between the cache key and the 
> fetched data.

# Extracted Query Functions

```csharp
// Extracted query function for reusability
async Task<List<string>> FetchTodosAsync(QueryFunctionContext ctx)
{
    var (queryKey, signal, meta) = ctx; // Destructure all properties!
    var status = (string?)queryKey[1];
    return await FakeApi.GetTodosAsync(status);
}

var query = new UseQuery<List<string>>(
    new QueryOptions<List<string>>(
        queryKey: new("todos", "active"),
        queryFn: FetchTodosAsync
    ),
    _queryClient
);
```

# Handling Errors

SwrSharp tracks query errors automatically. A query is considered failed if the function:
- Throws an exception, or
- Returns a faulted `Task<T>`

```csharp
var query = new UseQuery<string>(
    new QueryOptions<string>(
        queryKey: new("todo", todoId),
        queryFn: async ctx => {
            if (somethingGoesWrong)
                throw new Exception("Something went wrong");

            if (somethingElseGoesWrong)
                return await Task.FromException<string>(new("Something else went wrong"));

            return await FetchTodoByIdAsync(todoId);
        }
    ),
    _queryClient
);

await query.ExecuteAsync();

if (query.IsError)
    Console.WriteLine(query.Error!.Message);
```

# Destructuring Context

C# supports **deconstruction** (similar to JavaScript destructuring) for `QueryFunctionContext`:

```csharp
var (queryKey, signal, meta) = ctx;

// For infinite queries (includes pageParam):
var (queryKey, signal, meta, pageParam) = ctx;
```

**Benefits:**
- Cleaner code
- Extract only what you need
- More readable when you use multiple properties

**Examples:**

```csharp
// Destructure queryKey and signal only
queryFn: async ctx => {
    var (queryKey, signal) = ctx;
    var id = (int)queryKey[1]!;
    return await http.GetAsync($"/api/todo/{id}", signal);
}

// Destructure all properties (for regular queries)
queryFn: async ctx => {
    var (queryKey, signal, meta) = ctx;
    
    if (meta?.TryGetValue("includeDetails", out var include) == true && (bool)include)
        return await FetchDetailedTodoAsync(queryKey, signal);
    
    return await FetchBasicTodoAsync(queryKey, signal);
}

// Destructure with pageParam (for infinite queries)
queryFn: async ctx => {
    var (queryKey, signal, meta, pageParam) = ctx;
    var cursor = (int?)pageParam ?? 0;
    return await FetchProjectsAsync(cursor, signal);
}

// Ignore unused properties with discard
queryFn: async ctx => {
    var (queryKey, _) = ctx; // Ignore signal
    return await ProcessQueryKeyAsync(queryKey);
}
```

# Usage with HttpClient

Some HTTP clients (like `HttpClient`) do not automatically throw exceptions for non-success HTTP responses. 

In that case, you should check the response and throw manually:

```csharp
var query = new UseQuery<string>(
    new QueryOptions<string>(
        queryKey: new("todo", todoId),
        queryFn: async ctx => {
            var response = await http.GetAsync($"/api/todos/{todoId}");
            
            if (!response.IsSuccessStatusCode)
                throw new Exception($"Response was not ok: {response.StatusCode}");

            return await response.Content.ReadFromJsonAsync<string>()!;
        }
    ),
    _queryClient
);

await query.ExecuteAsync();
```

# Using Metadata

You can pass optional metadata via `QueryOptions.Meta` to provide additional context 
to your query function. This is useful for custom logic without cluttering the `QueryKey`:

```csharp
var query = new UseQuery<List<string>>(
    new QueryOptions<List<string>>(
        queryKey: new("todos"),
        queryFn: async ctx => {
            if (ctx.Meta?.TryGetValue("filter", out var filterValue) == true)
                return await FakeApi.GetFilteredTodosAsync((string)filterValue);
            
            return await FakeApi.GetTodosAsync();
        },
        meta: new Dictionary<string, object> { { "filter", "active" } }
    ),
    _queryClient
);
```

# Cancellation

You can pass a `CancellationToken` to `ExecuteAsync` to cancel a running query. The token 
is automatically passed to your query function via `QueryFunctionContext.Signal`, allowing you 
to propagate cancellation to underlying operations.

```csharp
var cts = new CancellationTokenSource();
await query.ExecuteAsync(cts.Token);

// Later, cancel if needed
cts.Cancel();
```

**Propagating the signal to HttpClient:**

```csharp
var query = new UseQuery<string>(
    new QueryOptions<string>(
        queryKey: new("todo", 1),
        queryFn: async ctx => {
            var response = await http.GetAsync("/api/todo/1", ctx.Signal);
            return await response.Content.ReadAsStringAsync();
        }
    ),
    _queryClient
);
```

