# BlazorQuery 🚀

A powerful asynchronous state management library for Blazor, inspired by [TanStack React Query](https://tanstack.com/query/latest).

## ✨ Features

- 🔄 **Automatic Background Refetching** - Keep your data fresh automatically
- 💾 **Smart Caching** - Efficient data caching with configurable stale times
- 🌐 **Network Mode Support** - Handle online/offline scenarios gracefully
- 🔁 **Retry Logic** - Automatic retry with exponential backoff
- ⚡ **Optimistic Updates** - Fast UI updates with background synchronization
- 🎯 **Type-Safe** - Full C# type safety with generics
- 🧵 **Thread-Safe** - Designed for concurrent access
- 📱 **Offline-First Ready** - Support for offline-first architectures

## 📚 Documentation

- [⚠️ Important Defaults](./21.%20Important%20Defaults.md) - **Start here!** Understand default behaviors
- [1. Query Keys](./1.%20Query%20Keys.md) - Learn about query identification and caching
- [2. Query Functions](./2.%20Query%20Functions.md) - Define your data fetching logic
- [3. Network Mode](./3.%20Network%20Mode.md) - Handle online/offline scenarios
- [4. Query Retries](./4.%20Query%20Retries.md) - Configure retry behavior
- [5. Query Options](./5.%20Query%20Options.md) - Reusable query configurations
- [6. Parallel Queries](./6.%20Parallel%20Queries.md) - Execute multiple queries in parallel
- [7. Dependent Queries](./7.%20Dependent%20Queries.md) - Serial queries that depend on previous results
- [8. Background Fetching Indicators](./8.%20Background%20Fetching%20Indicators.md) - Show loading states for background refetches
- [9. Disabling Queries](./9.%20Disabling%20Queries.md) - Control when queries execute with enabled option
- [10. Window Focus Refetching](./10.%20Window%20Focus%20Refetching.md) - Automatic refetch when window gains focus
- [11. Initial Query Data](./11.%20Initial%20Query%20Data.md) - Prepopulate queries with initial data
- [12. Placeholder Query Data](./12.%20Placeholder%20Query%20Data.md) - Show preview data while fetching actual data
- [13. Paginated Queries](./13.%20Paginated%20Queries.md) - Smooth pagination without loading flicker
- [14. Infinite Queries](./14.%20Infinite%20Queries.md) - Load more and infinite scroll patterns
- [15. Query Invalidation](./15.%20Query%20Invalidation.md) - Invalidate and refetch queries on demand
- [16. Filters](./16.%20Filters.md) - Advanced query filtering and matching
- [17. Query Cancellation](./17.%20Query%20Cancellation.md) - Cancel ongoing queries with CancellationToken
- [18. Scroll Restoration](./18.%20Scroll%20Restoration.md) - Automatic scroll position preservation
- [19. Default Query Function](./19.%20Default%20Query%20Function.md) - Shared query function for entire app
- [20. Render Optimizations](./20.%20Render%20Optimizations.md) - Optimize component re-rendering performance
- [22. Logging and Error Handling](./22.%20Logging%20and%20Error%20Handling.md) - Production-ready logging and diagnostics

## 🚀 Quick Start

### Installation

```bash
dotnet add package BlazorQuery.Core
```

### Basic Usage

```csharp
// Create a query client
var queryClient = new QueryClient();

// Define a query
var todosQuery = new UseQuery<List<Todo>>(
    new QueryOptions<List<Todo>>(
        queryKey: new("todos"),
        queryFn: async ctx => await FetchTodosAsync()
    ),
    queryClient
);

// Execute the query
await todosQuery.ExecuteAsync();

// Access the data
if (todosQuery.IsLoading)
{
    // Show loading state
}
else if (todosQuery.IsError)
{
    // Show error: todosQuery.Error
}
else if (todosQuery.IsSuccess)
{
    // Use data: todosQuery.Data
}
```

### With Parameters

```csharp
var todoQuery = new UseQuery<Todo>(
    new QueryOptions<Todo>(
        queryKey: new("todo", todoId),
        queryFn: async ctx => {
            var id = (int)ctx.QueryKey[1]!;
            return await FetchTodoByIdAsync(id);
        }
    ),
    queryClient
);

// Or with destructuring (JavaScript-like!)
var todoQuery = new UseQuery<Todo>(
    new QueryOptions<Todo>(
        queryKey: new("todo", todoId),
        queryFn: async ctx => {
            var (queryKey, signal) = ctx; // Destructure context
            var id = (int)queryKey[1]!;
            return await FetchTodoByIdAsync(id, signal);
        }
    ),
    queryClient
);
```

### Network Modes

```csharp
var query = new UseQuery<List<Todo>>(
    new QueryOptions<List<Todo>>(
        queryKey: new("todos"),
        queryFn: async ctx => await FetchTodosAsync(),
        networkMode: NetworkMode.Online, // or Always, OfflineFirst
        refetchOnReconnect: true
    ),
    queryClient
);
```

### Retry Configuration

```csharp
var query = new UseQuery<List<Todo>>(
    new QueryOptions<List<Todo>>(
        queryKey: new("todos"),
        queryFn: async ctx => await FetchTodosAsync(),
        retry: 3, // Max 3 total attempts
        retryDelay: TimeSpan.FromSeconds(1),
        maxRetryDelay: TimeSpan.FromSeconds(30)
    ),
    queryClient
);
```

### Stale Time & Background Refetching

```csharp
var query = new UseQuery<List<Todo>>(
    new QueryOptions<List<Todo>>(
        queryKey: new("todos"),
        queryFn: async ctx => await FetchTodosAsync(),
        staleTime: TimeSpan.FromMinutes(5), // Data fresh for 5 minutes
        refetchInterval: TimeSpan.FromMinutes(1) // Poll every minute
    ),
    queryClient
);
```

### Reusable Query Options

Create factory methods for better organization and reusability:

```csharp
// Define reusable query options
static QueryOptions<Todo> TodoOptions(int id)
{
    return new QueryOptions<Todo>(
        queryKey: new("todo", id),
        queryFn: async ctx => {
            var (queryKey, signal) = ctx;
            var todoId = (int)queryKey[1]!;
            return await FetchTodoAsync(todoId, signal);
        },
        staleTime: TimeSpan.FromMinutes(5)
    );
}

// Use everywhere
var query1 = new UseQuery<Todo>(TodoOptions(1), queryClient);
var query2 = new UseQuery<Todo>(TodoOptions(2), queryClient);
await queryClient.PrefetchQueryAsync(TodoOptions(3));
```

See [Query Options](./5.%20Query%20Options.md) for more details.

## 🎯 Key Concepts

### Query Status

Queries can be in one of three states:

- **Pending** - No data yet and no error
- **Success** - Has data and no error  
- **Error** - Has error (even if there's stale data)

### Fetch Status

Tracks the current fetch operation:

- **Idle** - Not currently fetching
- **Fetching** - Actively fetching data
- **Paused** - Fetch paused due to network conditions

### Loading States

- `IsLoading` - First load in progress (pending && (fetching || paused))
- `IsFetching` - Any fetch in progress
- `IsFetchingBackground` - Background refetch with existing data

## 🔍 React Query Compatibility

BlazorQuery closely follows React Query's behavior and patterns. Key differences:

- **Retry behavior**: `retry: 3` means 3 total attempts (React Query: 3 retries after initial = 4 total)
- **Language**: C# idioms instead of TypeScript/JavaScript

See [Copilot Instructions](./.github/copilot-instructions.md) for detailed compatibility notes.

## 🧪 Testing

```bash
dotnet test
```

All 40 tests should pass:
```
Passed!  - Failed:     0, Passed:    40, Skipped:     0, Total:    40
```

## 🛠️ Development

### Requirements

- .NET 8.0 or later
- C# 12.0 or later

### Project Structure

```
BlazorQuery/
├── src/
│   └── BlazorQuery.Core/        # Core library
├── tests/
│   └── BlazorQuery.Core.Tests/  # Unit tests
└── *.md                          # Documentation
```

## 📝 Recent Fixes

See [FIXES_APPLIED.md](./FIXES_APPLIED.md) for detailed information about recent improvements:

- ✅ Fixed QueryStatus logic to match React Query
- ✅ Fixed IsLoading definition to include Paused state
- ✅ Fixed thread safety issue with Random

## 🤝 Contributing

When contributing:

1. Check [React Query documentation](https://tanstack.com/query/latest) for expected behavior
2. Follow the patterns in [Copilot Instructions](./.github/copilot-instructions.md)
3. Ensure all tests pass
4. Update documentation if changing behavior
5. Add tests for new features

## 📄 License

[Your License Here]

## 🙏 Acknowledgments

Inspired by [TanStack Query](https://tanstack.com/query/latest) (formerly React Query).

---

**⚡ Built with performance and developer experience in mind.**

