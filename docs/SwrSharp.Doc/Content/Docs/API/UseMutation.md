---
title: "UseMutation"
description: "UseMutation API reference"
order: 3
category: "API"
---

# UseMutation

The `UseMutation` class is used to perform create, update, and delete operations.

## Usage

```csharp
var mutation = new UseMutation<TData, TVariables>(
    new MutationOptions<TData, TVariables>
    {
        MutationFn = async variables => await PerformMutation(variables),
        // ... other options
    },
    queryClient
);
```

## MutationOptions

### `MutationFn` (required)

`Func<TVariables, Task<TData>>`

The function that performs the mutation. Receives the variables passed to `Mutate()`/`MutateAsync()`.

```csharp
MutationFn = async (input) =>
{
    var response = await httpClient.PostAsJsonAsync("/api/todos", input);
    return await response.Content.ReadFromJsonAsync<Todo>();
}
```

### `MutationKey`

`QueryKey?` (optional)

An optional key to identify the mutation.

### `Retry`

`int` (default: `0`)

Number of times to retry on failure. Unlike queries (which default to 3), mutations default to 0 retries.

### `RetryDelay`

`TimeSpan?` (optional)

Fixed delay between retries. If not set, uses exponential backoff: `min(1000 * 2^attempt, maxRetryDelay)`.

### `RetryDelayFunc`

`Func<int, TimeSpan>?` (optional)

Custom function to compute retry delay. Receives the attempt index (0-based).

```csharp
RetryDelayFunc = attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt))
```

### `MaxRetryDelay`

`TimeSpan?` (default: 30 seconds)

Maximum delay between retries when using default exponential backoff.

### `NetworkMode`

`NetworkMode` (default: `NetworkMode.Online`)

Controls mutation behavior relative to network status.

- `Online` - Pauses when offline
- `Always` - Executes regardless of network status
- `OfflineFirst` - First attempt always executes

### `Meta`

`IReadOnlyDictionary<string, object>?` (optional)

Arbitrary metadata to attach to the mutation.

### `Scope`

`MutationScope?` (optional)

When set, mutations with the same `Scope.Id` run serially (one at a time).

```csharp
Scope = new MutationScope { Id = "todos" }
```

### `OnMutate`

`Func<TVariables, MutationContext, Task<object?>>?` (optional)

Called before the mutation function fires. The return value is passed to `OnSuccess`, `OnError`, and `OnSettled` as the `onMutateResult` parameter.

```csharp
OnMutate = async (variables, context) =>
{
    var snapshot = context.Client.Get<List<Todo>>(key);
    return snapshot; // Passed as onMutateResult
}
```

### `OnSuccess`

`Func<TData, TVariables, object?, MutationContext, Task>?` (optional)

Called when the mutation succeeds. Fires for **every** mutation call.

Parameters: `(data, variables, onMutateResult, context)`

### `OnError`

`Func<Exception, TVariables, object?, MutationContext, Task>?` (optional)

Called when the mutation fails. Fires for **every** mutation call.

Parameters: `(error, variables, onMutateResult, context)`

### `OnSettled`

`Func<TData?, Exception?, TVariables, object?, MutationContext, Task>?` (optional)

Called when the mutation settles (success or error). Fires for **every** mutation call.

Parameters: `(data, error, variables, onMutateResult, context)`

## MutateOptions (per-call)

Per-call callbacks passed to `Mutate()` or `MutateAsync()`. These only fire for the **last** mutation call.

### `OnSuccess`

`Func<TData, TVariables, object?, MutationContext, Task>?`

### `OnError`

`Func<Exception, TVariables, object?, MutationContext, Task>?`

### `OnSettled`

`Func<TData?, Exception?, TVariables, object?, MutationContext, Task>?`

## Return Value Properties

### `Data`

`TData?`

The last successfully resolved data for the mutation.

### `Error`

`Exception?`

The error object for the mutation, if it encountered an error.

### `Variables`

`TVariables?`

The variables passed to the most recent `Mutate()`/`MutateAsync()` call.

### `Status`

`MutationStatus`

The current status: `Idle`, `Pending`, `Error`, or `Success`.

### `IsIdle`

`bool` - `true` if the mutation is idle (fresh or reset state).

### `IsPending`

`bool` - `true` if the mutation is currently running.

### `IsError`

`bool` - `true` if the mutation encountered an error.

### `IsSuccess`

`bool` - `true` if the mutation completed successfully.

### `IsPaused`

`bool` - `true` if the mutation is paused (offline with `NetworkMode.Online`).

### `FailureCount`

`int`

The number of times the mutation has failed during the current call (incremented during retries).

### `FailureReason`

`Exception?`

The error from the most recent failed attempt (available during retries).

### `SubmittedAt`

`DateTime?`

The timestamp when the most recent mutation was submitted.

## Methods

### `Mutate(variables, options?)`

`void Mutate(TVariables variables, MutateOptions<TData, TVariables>? options = null)`

Fire-and-forget mutation. Errors are caught internally (use callbacks for error handling).

### `MutateAsync(variables, options?)`

`Task<TData> MutateAsync(TVariables variables, MutateOptions<TData, TVariables>? options = null)`

Awaitable mutation. Returns the mutation data on success, throws on error.

### `Reset()`

`void Reset()`

Resets the mutation to its initial idle state. Clears `Data`, `Error`, `Variables`, `SubmittedAt`, and `FailureCount`.

## Events

### `OnChange`

`event Action? OnChange`

Fired when the mutation state changes (status, data, error, etc.).

## MutationContext

The context object passed to all lifecycle callbacks.

### `Client`

`QueryClient`

The `QueryClient` instance. Use this to perform cache operations like invalidation.
