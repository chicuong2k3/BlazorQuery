---
title: "Updates from Mutation Responses"
description: "Using mutation responses to update query cache directly"
order: 18
category: "Guides"
---

# Updates from Mutation Responses

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

## Reusable Mutation Helper

You might want to tie the `OnSuccess` logic into a reusable mutation. For that you can create a helper method:

```csharp
UseMutation<Todo, EditTodoInput> CreateEditTodoMutation(QueryClient queryClient)
{
    return new UseMutation<Todo, EditTodoInput>(
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
}
```

## Updating a List from a Single Item Mutation

When you update a single item, you might also want to update the list query that contains it. You can do this by updating the list cache directly:

```csharp
var mutation = new UseMutation<Todo, EditTodoInput>(
    new MutationOptions<Todo, EditTodoInput>
    {
        MutationFn = async input => await EditTodo(input),
        OnSuccess = async (data, variables, onMutateResult, context) =>
        {
            // Update the detail query
            context.Client.SetQueryData(
                new QueryKey("todo", variables.Id),
                data
            );

            // Also update the item in the list cache
            var todos = context.Client.GetQueryData<List<Todo>>(new QueryKey("todos"));
            if (todos != null)
            {
                var updatedList = todos
                    .Select(t => t.Id == data.Id ? data : t)
                    .ToList();
                context.Client.SetQueryData(new QueryKey("todos"), updatedList);
            }
        }
    },
    queryClient
);
```

## Immutability

> **Important**: Updates via `SetQueryData` must be performed in an immutable way. **DO NOT** attempt to mutate cached data in place. Always create new objects or collections when updating the cache.

```csharp
// GOOD - create a new list
var updatedList = todos.Select(t => t.Id == id ? newTodo : t).ToList();
context.Client.SetQueryData(new QueryKey("todos"), updatedList);

// BAD - mutating in place
var todo = todos.First(t => t.Id == id);
todo.Title = "new title"; // Don't do this!
context.Client.SetQueryData(new QueryKey("todos"), todos);
```

## When to Use Direct Updates vs. Invalidation

| Approach | Use when |
|---|---|
| `SetQueryData` | The mutation response contains the full updated object. Avoids an extra network request. |
| `InvalidateQueries` | The mutation response doesn't return the updated data, or multiple queries need fresh data from the server. |
| Both | Update the cache immediately for instant UI feedback, then invalidate to ensure consistency with the server. |

```csharp
var mutation = new UseMutation<Todo, EditTodoInput>(
    new MutationOptions<Todo, EditTodoInput>
    {
        MutationFn = async input => await EditTodo(input),
        OnSuccess = async (data, variables, onMutateResult, context) =>
        {
            // Instant update for the detail view
            context.Client.SetQueryData(
                new QueryKey("todo", variables.Id),
                data
            );

            // Invalidate the list to refetch with correct ordering/filtering
            context.Client.InvalidateQueries(new QueryFilters
            {
                QueryKey = new QueryKey("todos")
            });
        }
    },
    queryClient
);
```
