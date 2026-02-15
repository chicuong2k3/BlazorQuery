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

## Invalidation from Mutations

The most common use case for mutations is to invalidate related queries so they refetch with fresh data:

```csharp
var mutation = new UseMutation<Todo, CreateTodoInput>(
    new MutationOptions<Todo, CreateTodoInput>
    {
        MutationFn = async input => await CreateTodo(input),
        OnSuccess = async (data, variables, onMutateResult, context) =>
        {
            // Invalidate all todo queries
            context.Client.InvalidateQueries(new QueryFilters
            {
                QueryKey = new QueryKey("todos")
            });
        }
    },
    queryClient
);
```

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
