
# What is a Query Function?

A **Query Function** is any asynchronous method that fetches data. 
It must either return **a result** or **throw an exception** so that BlazorQuery can track **loading** 
and **error** states.

Query functions receive a `QueryFunctionContext` that provides:
- `QueryKey`: uniquely identifies the query.
- `Signal`: a `CancellationToken` that allows query cancellation.
- `Meta`: optional metadata for the query.

# Basic Usage

You can define a query function in several ways depending on your needs:

```csharp
// Simple query without parameters
var query1 = new UseQuery<List<Todo>>(
    key: new QueryKey("todos"),
    fetchFn: async ctx => await FetchAllTodosAsync(),
    client: queryClient
);

// Query with a single parameter
var query2 = new UseQuery<Todo>(
    key: new QueryKey("todo", todoId),
    fetchFn: async ctx => await FetchTodoByIdAsync(todoId),
    client: queryClient
);

// Using QueryFunctionContext to access query key values
var query3 = new UseQuery<Todo>(
    key: new QueryKey("todo", todoId),
    fetchFn: async ctx => {
        var id = (int)ctx.QueryKey[1];
        return await FetchTodoByIdAsync(id);
    },
    client: queryClient
);
```

# Handling Errors

BlazorQuery tracks query errors automatically. A query is considered failed if the function:
- Throws an exception, or
- Returns a faulted `Task<T>`

```csharp
var query = new UseQuery<Todo>(
    key: new QueryKey("todo", todoId),
    fetchFn: async ctx => {
        if (somethingGoesWrong)
            throw new Exception("Something went wrong");

        if (somethingElseGoesWrong)
            return await Task.FromException<Todo>(new Exception("Something else went wrong"));

        return await FetchTodoByIdAsync(todoId);
    },
    client: queryClient
);

await query.ExecuteAsync();

if (query.IsError)
    Console.WriteLine(query.Error!.Message);
```

# Usage with HttpClient

Some HTTP clients (like `HttpClient`) do not automatically throw exceptions for non-success HTTP responses. 
In that case, you should check the response and throw manually:

```csharp
var query = new UseQuery<Todo>(
    key: new QueryKey("todo", todoId),
    fetchFn: async ctx => {
        var response = await http.GetAsync($"/api/todos/{todoId}");
        if (!response.IsSuccessStatusCode)
            throw new Exception($"Network response was not ok: {response.StatusCode}");

        return await response.Content.ReadFromJsonAsync<Todo>()!;
    },
    client: queryClient
);

await query.ExecuteAsync();
```

# Cancellation

You can pass a `CancellationToken` to `ExecuteAsync` to cancel a running query:

```csharp
var cts = new CancellationTokenSource();
await query.ExecuteAsync(cts.Token);

// Later, cancel if needed
cts.Cancel();
```

The token is automatically passed to your query function via `QueryFunctionContext.Signal`.