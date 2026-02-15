---
title: "Queries"
description: "Query basics and fundamentals"
order: 3
category: "GettingStarted"
---

# Query Basics

A query is a declarative dependency on an asynchronous source of data that is tied to a **unique key**. A query can be used with any async method (including GET and POST methods) to fetch data from a server.

> **Note**: If your method modifies data on the server, we recommend using [Mutations](/docs/Guides/Mutations) instead.

To create a query in SwrSharp, you need at least:

- A **unique key for the query** (`QueryKey`)
- A **function that returns a Task** that either:
  - Returns the data, or
  - Throws an exception

```csharp
using SwrSharp.Core;

var queryClient = new QueryClient();

var todosQuery = new UseQuery<List<Todo>>(
    new QueryOptions<List<Todo>>(
        queryKey: new("todos"),
        queryFn: async ctx => await FetchTodoListAsync()
    ),
    queryClient
);

await todosQuery.ExecuteAsync();
```

The **unique key** you provide is used internally for refetching, caching, and sharing your queries throughout your application.

## Query Result

The query instance contains all of the information about the query that you'll need for templating and any other usage of the data:

```csharp
var query = new UseQuery<List<Todo>>(
    new QueryOptions<List<Todo>>(
        queryKey: new("todos"),
        queryFn: async ctx => await FetchTodoListAsync()
    ),
    queryClient
);

await query.ExecuteAsync();

// Access query state
var data = query.Data;           // The fetched data
var error = query.Error;         // Any error that occurred
var status = query.Status;       // Pending, Error, or Success
var fetchStatus = query.FetchStatus; // Idle, Fetching, or Paused
```

## Query Status

A query can only be in one of the following states at any given moment:

| Status | Property | Description |
|--------|----------|-------------|
| `Pending` | `IsPending` or `Status == QueryStatus.Pending` | The query has no data yet |
| `Error` | `IsError` or `Status == QueryStatus.Error` | The query encountered an error |
| `Success` | `IsSuccess` or `Status == QueryStatus.Success` | The query was successful and data is available |

Beyond those primary states, more information is available depending on the state of the query:

- `Error` — If the query is in an `IsError` state, the error is available via the `Error` property.
- `Data` — If the query is in an `IsSuccess` state, the data is available via the `Data` property.
- `IsFetching` — In any state, if the query is fetching at any time (including background refetching) `IsFetching` will be `true`.

## Basic Usage Pattern

For **most** queries, it's usually sufficient to check for the `IsPending` state, then the `IsError` state, then finally, assume that the data is available and render the successful state:

```csharp
public class TodosComponent
{
    private readonly QueryClient _queryClient;
    private UseQuery<List<Todo>>? _todosQuery;

    public TodosComponent(QueryClient queryClient)
    {
        _queryClient = queryClient;
    }

    public async Task InitializeAsync()
    {
        _todosQuery = new UseQuery<List<Todo>>(
            new QueryOptions<List<Todo>>(
                queryKey: new("todos"),
                queryFn: async ctx => await FetchTodoListAsync()
            ),
            _queryClient
        );

        // Subscribe to changes
        _todosQuery.OnChange += Render;

        await _todosQuery.ExecuteAsync();
    }

    private void Render()
    {
        if (_todosQuery == null) return;

        if (_todosQuery.IsPending)
        {
            Console.WriteLine("Loading...");
            return;
        }

        if (_todosQuery.IsError)
        {
            Console.WriteLine($"Error: {_todosQuery.Error!.Message}");
            return;
        }

        // We can assume by this point that IsSuccess == true
        foreach (var todo in _todosQuery.Data!)
        {
            Console.WriteLine($"- {todo.Title}");
        }
    }
}
```

If booleans aren't your thing, you can always use the `Status` property as well:

```csharp
private void Render()
{
    if (_todosQuery == null) return;

    switch (_todosQuery.Status)
    {
        case QueryStatus.Pending:
            Console.WriteLine("Loading...");
            break;
        case QueryStatus.Error:
            Console.WriteLine($"Error: {_todosQuery.Error!.Message}");
            break;
        case QueryStatus.Success:
            foreach (var todo in _todosQuery.Data!)
            {
                Console.WriteLine($"- {todo.Title}");
            }
            break;
    }
}
```

## FetchStatus

In addition to the `Status` property, you will also get an additional `FetchStatus` property with the following options:

| FetchStatus | Property | Description |
|-------------|----------|-------------|
| `Fetching` | `FetchStatus == FetchStatus.Fetching` | The query is currently fetching |
| `Paused` | `FetchStatus == FetchStatus.Paused` | The query wanted to fetch, but it is paused (see [Network Mode](../Guides/03-Network-Mode)) |
| `Idle` | `FetchStatus == FetchStatus.Idle` | The query is not doing anything at the moment |

## Why Two Different States?

Background refetches and stale-while-revalidate logic make all combinations for `Status` and `FetchStatus` possible. For example:

- A query in `Success` status will usually be in `Idle` fetchStatus, but it could also be in `Fetching` if a background refetch is happening.
- A query that starts with no data will usually be in `Pending` status and `Fetching` fetchStatus, but it could also be `Paused` if there is no network connection.

So keep in mind that a query can be in `Pending` state without actually fetching data. As a rule of thumb:

> - The **`Status`** gives information about the **data**: Do we have any or not?
> - The **`FetchStatus`** gives information about the **queryFn**: Is it running or not?

## Convenience Properties

SwrSharp provides several convenience boolean properties:

| Property | Equivalent | Description |
|----------|------------|-------------|
| `IsPending` | `Status == QueryStatus.Pending` | No data yet |
| `IsSuccess` | `Status == QueryStatus.Success` | Has data |
| `IsError` | `Status == QueryStatus.Error` | Has error |
| `IsFetching` | `FetchStatus == FetchStatus.Fetching` | Currently fetching |
| `IsPaused` | `FetchStatus == FetchStatus.Paused` | Paused (offline) |
| `IsLoading` | `IsPending && (IsFetching \|\| IsPaused)` | First load in progress |
| `IsFetchingBackground` | `IsFetching && Data != null` | Background refetch with existing data |

## Reactive Updates with OnChange

Unlike React Query's hooks which automatically re-render, SwrSharp requires you to subscribe to changes:

```csharp
var query = new UseQuery<List<Todo>>(options, queryClient);

// Subscribe to state changes
query.OnChange += () => {
    Console.WriteLine($"Status: {query.Status}, IsFetching: {query.IsFetching}");
    // Update your UI here
};

await query.ExecuteAsync();
```

## Comparison with React Query

### React Query (TypeScript):
```typescript
function Todos() {
  const { isPending, isError, data, error } = useQuery({
    queryKey: ['todos'],
    queryFn: fetchTodoList,
  })

  if (isPending) return <span>Loading...</span>
  if (isError) return <span>Error: {error.message}</span>

  return (
    <ul>
      {data.map((todo) => (
        <li key={todo.id}>{todo.title}</li>
      ))}
    </ul>
  )
}
```

### SwrSharp (C#):
```csharp
public class TodosComponent
{
    private UseQuery<List<Todo>>? _query;

    public async Task InitializeAsync(QueryClient queryClient)
    {
        _query = new UseQuery<List<Todo>>(
            new QueryOptions<List<Todo>>(
                queryKey: new("todos"),
                queryFn: async ctx => await FetchTodoListAsync()
            ),
            queryClient
        );

        _query.OnChange += Render;
        await _query.ExecuteAsync();
    }

    private void Render()
    {
        if (_query!.IsPending)
        {
            Console.WriteLine("Loading...");
            return;
        }

        if (_query.IsError)
        {
            Console.WriteLine($"Error: {_query.Error!.Message}");
            return;
        }

        foreach (var todo in _query.Data!)
        {
            Console.WriteLine($"- {todo.Title}");
        }
    }
}
```

**Key Differences**:
- React Query hooks automatically execute and re-render — SwrSharp requires explicit `ExecuteAsync()` and `OnChange` subscription
- React Query uses destructuring — SwrSharp accesses properties directly on the query object
- Both provide the same status/fetchStatus model for consistent state management

## Next Steps

- Learn about [Query Keys](../Guides/01-Query-Keys) for organizing your queries
- Learn about [Query Functions](../Guides/02-Query-Functions) for fetching data
- Learn about [Network Mode](../Guides/03-Network-Mode) for offline support
- Learn about [Query Retries](../Guides/04-Query-Retries) for error handling

