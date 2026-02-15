---
title: "UseMutation"
description: "UseMutation API reference"
order: 4
category: "API"
---

# UseMutation

The `UseMutation` class is used to create, update, or delete data and perform server side-effects.

## Usage

```csharp
var mutation = new UseMutation<Todo, CreateTodoInput>(
    new MutationOptions<Todo, CreateTodoInput>
    {
        MutationFn = async input => await PostTodo(input)
    },
    queryClient
);

// Fire-and-forget
mutation.Mutate(new CreateTodoInput { Title = "New Todo" });

// Or await the result
var newTodo = await mutation.MutateAsync(new CreateTodoInput { Title = "New Todo" });
```

## Type Parameters

| Parameter | Description |
|---|---|
| `TData` | The type of data returned by the mutation function |
| `TVariables` | The type of variables passed to the mutation function |

## Constructor

```csharp
new UseMutation<TData, TVariables>(MutationOptions<TData, TVariables> options, QueryClient client)
```

## MutationOptions

| Option | Type | Default | Description |
|---|---|---|---|
| `MutationFn` | `Func<TVariables, Task<TData>>` | **required** | The mutation function |
| `MutationKey` | `QueryKey?` | `null` | Optional key to identify the mutation |
| `Retry` | `int` | `0` | Number of retry attempts on failure |
| `RetryDelay` | `TimeSpan?` | `null` | Fixed delay between retries |
| `RetryDelayFunc` | `Func<int, TimeSpan>?` | `null` | Custom retry delay function |
| `MaxRetryDelay` | `TimeSpan?` | 30s | Maximum retry delay (exponential backoff) |
| `NetworkMode` | `NetworkMode` | `Online` | Network behavior |
| `Meta` | `IReadOnlyDictionary<string, object>?` | `null` | Arbitrary metadata |
| `Scope` | `MutationScope?` | `null` | Scope for serial execution |
| `OnMutate` | `Func<TVariables, MutationContext, Task<object?>>?` | `null` | Called before mutation fires |
| `OnSuccess` | `Func<TData, TVariables, object?, MutationContext, Task>?` | `null` | Called on success |
| `OnError` | `Func<Exception, TVariables, object?, MutationContext, Task>?` | `null` | Called on error |
| `OnSettled` | `Func<TData?, Exception?, TVariables, object?, MutationContext, Task>?` | `null` | Called on success or error |

## Properties

| Property | Type | Description |
|---|---|---|
| `Data` | `TData?` | Last successfully resolved data |
| `Error` | `Exception?` | Error from failed mutation |
| `Variables` | `TVariables?` | Variables from most recent call |
| `Status` | `MutationStatus` | `Idle`, `Pending`, `Error`, or `Success` |
| `IsIdle` | `bool` | Mutation is idle (fresh or reset) |
| `IsPending` | `bool` | Mutation is currently running |
| `IsError` | `bool` | Mutation encountered an error |
| `IsSuccess` | `bool` | Mutation was successful |
| `IsPaused` | `bool` | Mutation is paused (offline) |
| `FailureCount` | `int` | Number of failed attempts during retries |
| `FailureReason` | `Exception?` | Error from most recent failed attempt |
| `SubmittedAt` | `DateTime?` | Timestamp of mutation submission |

## Methods

### `Mutate`

```csharp
void Mutate(TVariables variables, MutateOptions<TData, TVariables>? options = null)
```

Fire-and-forget mutation. Errors are swallowed — use callbacks for error handling.

### `MutateAsync`

```csharp
Task<TData> MutateAsync(TVariables variables, MutateOptions<TData, TVariables>? options = null)
```

Awaitable mutation that returns data on success or throws on error.

### `Reset`

```csharp
void Reset()
```

Resets the mutation to its initial idle state, clearing `Data`, `Error`, `Variables`, and all tracking properties.

## Events

| Event | Type | Description |
|---|---|---|
| `OnChange` | `Action?` | Fired whenever mutation state changes |

## MutateOptions (Per-Call)

Per-call callbacks passed to `Mutate()` or `MutateAsync()`. These only fire for the **last** call when multiple calls happen in quick succession.

| Option | Type | Description |
|---|---|---|
| `OnSuccess` | `Func<TData, TVariables, object?, MutationContext, Task>?` | Per-call success callback |
| `OnError` | `Func<Exception, TVariables, object?, MutationContext, Task>?` | Per-call error callback |
| `OnSettled` | `Func<TData?, Exception?, TVariables, object?, MutationContext, Task>?` | Per-call settled callback |

## MutationContext

All callbacks receive a `MutationContext` that provides access to the `QueryClient`:

```csharp
OnSuccess = async (data, variables, onMutateResult, context) =>
{
    // Access QueryClient via context
    context.Client.InvalidateQueries(new QueryFilters
    {
        QueryKey = new QueryKey("todos")
    });
}
```

| Property | Type | Description |
|---|---|---|
| `Client` | `QueryClient` | The QueryClient instance for cache operations |

## MutationScope

Mutations with the same scope run serially (one at a time):

```csharp
Scope = new MutationScope { Id = "todos" }
```

| Property | Type | Description |
|---|---|---|
| `Id` | `string` | Unique scope identifier |

## Example

```csharp
var mutation = new UseMutation<Todo, CreateTodoInput>(
    new MutationOptions<Todo, CreateTodoInput>
    {
        MutationFn = async input =>
        {
            var response = await httpClient.PostAsJsonAsync("/api/todos", input);
            return await response.Content.ReadFromJsonAsync<Todo>();
        },
        OnSuccess = async (data, variables, onMutateResult, context) =>
        {
            context.Client.InvalidateQueries(new QueryFilters
            {
                QueryKey = new QueryKey("todos")
            });
        },
        Retry = 2,
        RetryDelay = TimeSpan.FromSeconds(1)
    },
    queryClient
);

mutation.OnChange += () => StateHasChanged();

mutation.Mutate(new CreateTodoInput { Title = "Buy groceries" });

if (mutation.IsPending)
{
    // Show loading state
}
else if (mutation.IsSuccess)
{
    // Show success: mutation.Data
}
else if (mutation.IsError)
{
    // Show error: mutation.Error.Message
}
```

## See Also

- [Mutations Guide](/docs/Guides/Mutations)
- [Invalidations from Mutations](/docs/Guides/Invalidations-from-Mutations)
- [Updates from Mutation Responses](/docs/Guides/Updates-from-Mutation-Responses)
- [QueryClient](/docs/API/QueryClient)
