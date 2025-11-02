


In BlazorQuery, a Query Function is simply any Func<Task<T>> that fetches data asynchronously — usually from an API or database.
It must either return a result or throw an exception for BlazorQuery to manage its loading and error states properly.

# Basic Usage

You define a query function inside your UseQuery<T> call.
Here are all the valid patterns:

```
await useQuery.UseQuery(
    new QueryOptions<List<Todo>> {
        QueryKey = new QueryKey("todos"),
        QueryFn = () => FetchAllTodosAsync()
    }
);

await useQuery.UseQuery(
    new QueryOptions<Todo> {
        QueryKey = new QueryKey("todo", todoId),
        QueryFn = () => FetchTodoByIdAsync(todoId)
    }
);

await useQuery.UseQuery(
    new QueryOptions<Todo> {
        QueryKey = new QueryKey("todo", todoId),
        QueryFn = async () => {
            var data = await FetchTodoByIdAsync(todoId);
            return data;
        }
    }
);

// Access queryKey values inside queryFn
await useQuery.UseQuery(
    new QueryOptions<Todo> {
        QueryKey = new QueryKey("todo", todoId),
        QueryFn = (ctx) => FetchTodoByIdAsync((int)ctx.QueryKey[1])
    }
);
```

# Handling and Throwing Errors

To mark a query as failed, throw an exception or return a faulted Task.
BlazorQuery automatically tracks and exposes this via the .Error and .IsError states.

```
var result = await useQuery.UseQuery(
    new QueryOptions<Todo> {
        QueryKey = new QueryKey("todo", todoId),
        QueryFn = async () => {
            if (somethingGoesWrong)
                throw new Exception("Oh no!");

            if (somethingElseGoesWrong)
                return await Task.FromException<Todo>(new Exception("Oh no!"));

            return await FetchTodoByIdAsync(todoId);
        }
    }
);

if (result.IsError)
    Console.WriteLine(result.Error.Message);

```

# Usage with HttpClient (like fetch)

Unlike libraries such as HttpClientFactory or Refit, the built-in HttpClient doesn’t throw exceptions for failed responses —
you’ll need to handle that manually:

```
var result = await useQuery.UseQuery(
    new QueryOptions<Todo> {
        QueryKey = new QueryKey("todo", todoId),
        QueryFn = async () => {
            var response = await http.GetAsync($"/api/todos/{todoId}");
            if (!response.IsSuccessStatusCode)
                throw new Exception($"Network response was not ok: {response.StatusCode}");
            
            return await response.Content.ReadFromJsonAsync<Todo>();
        }
    }
);

```

Query Function Variables

Query Keys in BlazorQuery are passed into your query function context,
so you can extract parameters dynamically.

```
await useQuery.UseQuery(
    new QueryOptions<List<Todo>> {
        QueryKey = new QueryKey("todos", new { Status = status, Page = page }),
        QueryFn = FetchTodoListAsync
    }
);

public Task<List<Todo>> FetchTodoListAsync(QueryFunctionContext ctx)
{
    var (_, options) = ctx.Deconstruct<(string, dynamic)>();
    var status = options.Status;
    var page = options.Page;

    return http.GetFromJsonAsync<List<Todo>>($"/api/todos?status={status}&page={page}");
}

```

# QueryFunctionContext

Every query function receives a QueryFunctionContext object with useful info:

Property	Type	Description
QueryKey	QueryKey	The unique identifier for this query
Signal	CancellationToken?	Used for canceling queries
Meta	Dictionary<string, object>?	Optional metadata about the query

```
public async Task<List<Todo>> FetchTodoListAsync(QueryFunctionContext ctx)
{
    var (_, filter) = ctx.Deconstruct<(string, dynamic)>();
    var status = filter.Status;
    var page = filter.Page;

    ctx.Signal?.ThrowIfCancellationRequested();

    return await http.GetFromJsonAsync<List<Todo>>($"/api/todos?status={status}&page={page}", ctx.Signal ?? CancellationToken.None);
}
```

Summary

✅ A query function is any async function returning data or throwing errors
✅ Always include variables (like IDs, filters) in your QueryKey
✅ Use QueryFunctionContext to access key values and cancellation tokens
✅ Errors must be thrown or returned as rejected tasks