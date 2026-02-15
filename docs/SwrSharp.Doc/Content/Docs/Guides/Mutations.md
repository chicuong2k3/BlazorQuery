---
title: "Mutations"
description: "Performing create, update, and delete operations"
order: 16
category: "Guides"
---

# Mutations

Unlike queries, mutations are typically used to create/update/delete data or perform server side-effects. For this purpose, SwrSharp provides a `UseMutation` class.

## Basic Mutation

```csharp
var mutation = new UseMutation<Todo, CreateTodoInput>(
    new MutationOptions<Todo, CreateTodoInput>
    {
        MutationFn = async input =>
        {
            var response = await httpClient.PostAsJsonAsync("/api/todos", input);
            return await response.Content.ReadFromJsonAsync<Todo>();
        }
    },
    queryClient
);

// Fire-and-forget
mutation.Mutate(new CreateTodoInput { Title = "New Todo" });

// Or await the result
var newTodo = await mutation.MutateAsync(new CreateTodoInput { Title = "New Todo" });
```

## Mutation States

A mutation can only be in one of the following states at any given moment:

- `IsIdle` - The mutation is idle or in a fresh/reset state
- `IsPending` - The mutation is currently running
- `IsError` - The mutation encountered an error
- `IsSuccess` - The mutation was successful and mutation data is available

Beyond these primary states, more information is available depending on the state of the mutation:

- `Error` - If the mutation is in an error state, the error is available via the `Error` property
- `Data` - If the mutation is in a success state, the data is available via the `Data` property

```csharp
var mutation = new UseMutation<Todo, CreateTodoInput>(
    new MutationOptions<Todo, CreateTodoInput>
    {
        MutationFn = async input => await CreateTodo(input)
    },
    queryClient
);

if (mutation.IsPending)
{
    // Show loading spinner
}
else if (mutation.IsError)
{
    Console.WriteLine($"Error: {mutation.Error!.Message}");
}
else if (mutation.IsSuccess)
{
    Console.WriteLine($"Created: {mutation.Data!.Title}");
}
```

## Resetting Mutation State

Sometimes you need to clear the error or data of a mutation request. To do this, you can use the `Reset` method:

```csharp
mutation.Reset();
// mutation.Status is now Idle
// mutation.Data is null
// mutation.Error is null
```

## Mutation Side Effects

`UseMutation` comes with powerful helper callbacks that allow quick and easy side-effects at any stage during the mutation lifecycle.

```csharp
var mutation = new UseMutation<Todo, CreateTodoInput>(
    new MutationOptions<Todo, CreateTodoInput>
    {
        MutationFn = async input => await CreateTodo(input),
        OnMutate = async (variables, context) =>
        {
            // Called before the mutation function fires
            // Return value is passed to other callbacks as onMutateResult
            Console.WriteLine("About to create todo...");
            return null;
        },
        OnSuccess = async (data, variables, onMutateResult, context) =>
        {
            Console.WriteLine($"Todo created: {data.Title}");
            // Invalidate and refetch related queries
            context.Client.InvalidateQueries(new QueryFilters
            {
                QueryKey = new QueryKey("todos")
            });
        },
        OnError = async (error, variables, onMutateResult, context) =>
        {
            Console.WriteLine($"Error creating todo: {error.Message}");
        },
        OnSettled = async (data, error, variables, onMutateResult, context) =>
        {
            // Called on both success and error
            Console.WriteLine("Mutation finished");
        }
    },
    queryClient
);
```

All callbacks receive a `MutationContext` that gives you access to the `QueryClient`, making it easy to perform cache operations like invalidation directly from your mutation callbacks.

## Per-Call Callbacks

In addition to the callbacks defined on `MutationOptions`, you can also pass callbacks when calling `Mutate()` or `MutateAsync()`. These per-call callbacks only fire for the **last** `Mutate()` call when multiple calls happen in quick succession.

```csharp
mutation.Mutate(
    new CreateTodoInput { Title = "New Todo" },
    new MutateOptions<Todo, CreateTodoInput>
    {
        OnSuccess = async (data, variables, onMutateResult, context) =>
        {
            // This only fires if this is the most recent Mutate() call
            Console.WriteLine("This specific mutation succeeded!");
        }
    }
);
```

### Callback Execution Order

When both option-level and per-call callbacks are defined:

1. `OnMutate` (option-level) - fires first
2. Mutation function executes
3. `OnSuccess`/`OnError` (option-level) - fires first
4. `OnSuccess`/`OnError` (per-call) - fires second
5. `OnSettled` (option-level) - fires first
6. `OnSettled` (per-call) - fires second

**Important:** Option-level callbacks fire for **every** mutation call. Per-call callbacks only fire for the **last** call.

## Consecutive Mutations

When `Mutate()` is called multiple times, each call starts a new mutation. The `Variables`, `Data`, `Error`, and `Status` properties always reflect the most recent call.

```csharp
mutation.Mutate(input1); // Starts mutation 1
mutation.Mutate(input2); // Starts mutation 2 (mutation 1 still runs)

// mutation.Variables is now input2
// Option-level callbacks fire for BOTH mutations
// Per-call callbacks only fire for mutation 2 (the last one)
```

## MutateAsync

While `Mutate()` is fire-and-forget, `MutateAsync()` returns a Task that you can await:

```csharp
try
{
    var data = await mutation.MutateAsync(input);
    Console.WriteLine($"Success: {data}");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
```

## Retry

By default, mutations do **not** retry on failure (unlike queries which retry 3 times). You can configure retry behavior:

```csharp
var mutation = new UseMutation<Todo, CreateTodoInput>(
    new MutationOptions<Todo, CreateTodoInput>
    {
        MutationFn = async input => await CreateTodo(input),
        Retry = 3, // Retry up to 3 times
        RetryDelay = TimeSpan.FromMilliseconds(1000), // Fixed delay
        // Or use exponential backoff:
        // RetryDelayFunc = attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
        MaxRetryDelay = TimeSpan.FromSeconds(30)
    },
    queryClient
);
```

During retries, `FailureCount` and `FailureReason` track the current retry state.

## Invalidations from Mutations

Invalidating queries is only half the battle. Knowing **when** to invalidate them is the other half. Usually when a mutation in your app succeeds, it's VERY likely that there are related queries in your application that need to be invalidated and possibly refetched to account for the new changes from your mutation.

For example, assume we have a mutation to post a new todo:

```csharp
var mutation = new UseMutation<Todo, CreateTodoInput>(
    new MutationOptions<Todo, CreateTodoInput>
    {
        MutationFn = async input => await PostTodo(input)
    },
    queryClient
);
```

When a successful `PostTodo` mutation happens, we likely want all `todos` queries to get invalidated and possibly refetched to show the new todo item. To do this, you can use `UseMutation`'s `OnSuccess` callback and the client's `InvalidateQueries` method:

```csharp
var mutation = new UseMutation<Todo, CreateTodoInput>(
    new MutationOptions<Todo, CreateTodoInput>
    {
        MutationFn = async input => await AddTodo(input),
        OnSuccess = async (data, variables, onMutateResult, context) =>
        {
            // Invalidate a single query
            context.Client.InvalidateQueries(new QueryFilters
            {
                QueryKey = new QueryKey("todos")
            });

            // Or invalidate multiple queries
            context.Client.InvalidateQueries(new QueryFilters
            {
                QueryKey = new QueryKey("todos")
            });
            context.Client.InvalidateQueries(new QueryFilters
            {
                QueryKey = new QueryKey("reminders")
            });
        }
    },
    queryClient
);
```

You can wire up your invalidations to happen using any of the callbacks available in `UseMutation` (`OnSuccess`, `OnError`, `OnSettled`).

## Updates from Mutation Responses

When dealing with mutations that **update** objects on the server, it's common for the new object to be automatically returned in the response of the mutation. Instead of refetching any queries for that item and wasting a network call for data we already have, we can take advantage of the object returned by the mutation function and update the existing query with the new data immediately using `QueryClient.SetQueryData`:

```csharp
var mutation = new UseMutation<Todo, EditTodoInput>(
    new MutationOptions<Todo, EditTodoInput>
    {
        MutationFn = async input => await EditTodo(input),
        OnSuccess = async (data, variables, onMutateResult, context) =>
        {
            context.Client.SetQueryData(
                new QueryKey("todo", variables.Id),
                data
            );
        }
    },
    queryClient
);

await mutation.MutateAsync(new EditTodoInput { Id = 5, Name = "Do the laundry" });

// The query below will be updated with the response from the
// successful mutation
var query = new UseQuery<Todo>(new QueryOptions<Todo>(
    queryKey: new QueryKey("todo", 5),
    queryFn: async ctx => await FetchTodoById(5)
), queryClient);
```

You might want to tie the `OnSuccess` logic into a reusable mutation. For that you can create a helper method:

```csharp
UseMutation<Todo, EditTodoInput> CreateEditTodoMutation(QueryClient queryClient)
{
    return new UseMutation<Todo, EditTodoInput>(
        new MutationOptions<Todo, EditTodoInput>
        {
            MutationFn = async input => await EditTodo(input),
            // Notice the second parameter gives you access to the variables
            OnSuccess = async (data, variables, onMutateResult, context) =>
            {
                context.Client.SetQueryData(
                    new QueryKey("todo", variables.Id),
                    data
                );
            }
        },
        queryClient
    );
}
```

> **Immutability**: Updates via `SetQueryData` should be performed in an immutable way. **DO NOT** attempt to mutate cached data in place. Always create new objects or collections when updating the cache.

## Optimistic Updates

You can perform optimistic updates using the `OnMutate` callback to update the cache before the mutation completes, and roll back in `OnError` if the mutation fails:

```csharp
var mutation = new UseMutation<Todo, UpdateTodoInput>(
    new MutationOptions<Todo, UpdateTodoInput>
    {
        MutationFn = async input => await UpdateTodo(input),
        OnMutate = async (variables, context) =>
        {
            // Snapshot the previous value
            var previousTodos = context.Client.Get<List<Todo>>(new QueryKey("todos"));

            // Optimistically update the cache
            var updated = previousTodos?.ToList() ?? new List<Todo>();
            var index = updated.FindIndex(t => t.Id == variables.Id);
            if (index >= 0)
                updated[index] = new Todo { Id = variables.Id, Title = variables.Title };
            context.Client.Set(new QueryKey("todos"), updated);

            // Return snapshot for rollback
            return previousTodos;
        },
        OnError = async (error, variables, onMutateResult, context) =>
        {
            // Roll back to the previous value
            if (onMutateResult is List<Todo> previousTodos)
            {
                context.Client.Set(new QueryKey("todos"), previousTodos);
            }
        },
        OnSettled = async (data, error, variables, onMutateResult, context) =>
        {
            // Always refetch after error or success
            context.Client.InvalidateQueries(new QueryFilters
            {
                QueryKey = new QueryKey("todos")
            });
        }
    },
    queryClient
);
```

## Mutation Scopes

Mutations with the same scope run serially. This is useful when you need to ensure mutations are processed in order:

```csharp
var mutation = new UseMutation<Todo, CreateTodoInput>(
    new MutationOptions<Todo, CreateTodoInput>
    {
        MutationFn = async input => await CreateTodo(input),
        Scope = new MutationScope { Id = "todos" }
    },
    queryClient
);

// These will run one after the other, not in parallel
mutation.Mutate(input1);
mutation.Mutate(input2);
mutation.Mutate(input3);
```

## MutationOptions Reference

| Option | Type | Default | Description |
|---|---|---|---|
| `MutationFn` | `Func<TVariables, Task<TData>>` | **required** | The mutation function |
| `MutationKey` | `QueryKey?` | `null` | Optional key to identify the mutation |
| `Retry` | `int` | `0` | Number of retry attempts on failure |
| `RetryDelay` | `TimeSpan?` | `null` | Fixed delay between retries |
| `RetryDelayFunc` | `Func<int, TimeSpan>?` | `null` | Custom retry delay function |
| `MaxRetryDelay` | `TimeSpan?` | 30s | Maximum retry delay (exponential backoff) |
| `NetworkMode` | `NetworkMode` | `Online` | Network behavior (`Online`, `Always`, `OfflineFirst`) |
| `Meta` | `IReadOnlyDictionary<string, object>?` | `null` | Arbitrary metadata |
| `Scope` | `MutationScope?` | `null` | Scope for serial execution |
| `OnMutate` | `Func<TVariables, MutationContext, Task<object?>>?` | `null` | Called before mutation fires |
| `OnSuccess` | `Func<TData, TVariables, object?, MutationContext, Task>?` | `null` | Called on success |
| `OnError` | `Func<Exception, TVariables, object?, MutationContext, Task>?` | `null` | Called on error |
| `OnSettled` | `Func<TData?, Exception?, TVariables, object?, MutationContext, Task>?` | `null` | Called on success or error |

## UseMutation Properties

| Property | Type | Description |
|---|---|---|
| `Data` | `TData?` | Last successfully resolved data |
| `Error` | `Exception?` | Error from failed mutation |
| `Variables` | `TVariables?` | Variables from most recent call |
| `Status` | `MutationStatus` | `Idle`, `Pending`, `Error`, or `Success` |
| `IsIdle` | `bool` | Mutation is idle (fresh or reset) |
| `IsPending` | `bool` | Mutation is running |
| `IsError` | `bool` | Mutation failed |
| `IsSuccess` | `bool` | Mutation succeeded |
| `IsPaused` | `bool` | Mutation is paused (offline) |
| `FailureCount` | `int` | Number of failed attempts during retries |
| `FailureReason` | `Exception?` | Error from most recent failed attempt |
| `SubmittedAt` | `DateTime?` | Timestamp of mutation submission |

## UseMutation Methods

| Method | Signature | Description |
|---|---|---|
| `Mutate` | `void Mutate(TVariables, MutateOptions?)` | Fire-and-forget mutation |
| `MutateAsync` | `Task<TData> MutateAsync(TVariables, MutateOptions?)` | Awaitable mutation (throws on error) |
| `Reset` | `void Reset()` | Reset to idle state |
