---
title: "Invalidations from Mutations"
description: "Invalidating and refetching queries when mutations succeed"
order: 17
category: "Guides"
---

# Invalidations from Mutations

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
        MutationFn = async input => await PostTodo(input),
        OnSuccess = async (data, variables, onMutateResult, context) =>
        {
            // Invalidate and refetch
            context.Client.InvalidateQueries(new QueryFilters
            {
                QueryKey = new QueryKey("todos")
            });
        }
    },
    queryClient
);
```

You can wire up your invalidations to happen using **any** of the callbacks available in `UseMutation`:

```csharp
var mutation = new UseMutation<Todo, CreateTodoInput>(
    new MutationOptions<Todo, CreateTodoInput>
    {
        MutationFn = async input => await PostTodo(input),
        OnSuccess = async (data, variables, onMutateResult, context) =>
        {
            context.Client.InvalidateQueries(new QueryFilters
            {
                QueryKey = new QueryKey("todos")
            });
        },
        OnError = async (error, variables, onMutateResult, context) =>
        {
            Console.WriteLine($"Mutation failed: {error.Message}");
        },
        OnSettled = async (data, error, variables, onMutateResult, context) =>
        {
            // Runs on both success and error
            context.Client.InvalidateQueries(new QueryFilters
            {
                QueryKey = new QueryKey("todos")
            });
        }
    },
    queryClient
);
```

All callbacks receive a `MutationContext` that gives you access to the `QueryClient` through `context.Client`, making it easy to perform cache operations directly from your mutation callbacks.

## Invalidating Multiple Queries

When a mutation affects multiple types of data, you can invalidate several queries at once:

```csharp
var mutation = new UseMutation<Todo, CreateTodoInput>(
    new MutationOptions<Todo, CreateTodoInput>
    {
        MutationFn = async input => await PostTodo(input),
        OnSuccess = async (data, variables, onMutateResult, context) =>
        {
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

## Targeted Invalidation Using Variables

You can use the `variables` parameter from the callback to perform more targeted invalidation:

```csharp
var mutation = new UseMutation<Todo, UpdateTodoInput>(
    new MutationOptions<Todo, UpdateTodoInput>
    {
        MutationFn = async input => await UpdateTodo(input),
        OnSuccess = async (data, variables, onMutateResult, context) =>
        {
            // Invalidate the specific todo detail query
            context.Client.InvalidateQueries(new QueryFilters
            {
                QueryKey = new QueryKey("todo", variables.Id),
                Exact = true
            });

            // Also invalidate the list so it reflects the update
            context.Client.InvalidateQueries(new QueryFilters
            {
                QueryKey = new QueryKey("todos")
            });
        }
    },
    queryClient
);
```

## Using `OnSettled` for Guaranteed Freshness

If you want queries to always be refetched regardless of whether the mutation succeeds or fails (e.g., for optimistic updates), use `OnSettled`:

```csharp
var mutation = new UseMutation<Todo, UpdateTodoInput>(
    new MutationOptions<Todo, UpdateTodoInput>
    {
        MutationFn = async input => await UpdateTodo(input),
        OnMutate = async (variables, context) =>
        {
            var previous = context.Client.GetQueryData<List<Todo>>(new QueryKey("todos"));
            // ... perform optimistic update ...
            return previous;
        },
        OnError = async (error, variables, onMutateResult, context) =>
        {
            if (onMutateResult is List<Todo> previous)
            {
                context.Client.SetQueryData(new QueryKey("todos"), previous);
            }
        },
        OnSettled = async (data, error, variables, onMutateResult, context) =>
        {
            // Always refetch to ensure consistency
            context.Client.InvalidateQueries(new QueryFilters
            {
                QueryKey = new QueryKey("todos")
            });
        }
    },
    queryClient
);
```
