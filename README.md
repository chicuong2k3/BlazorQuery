# SwrSharp 🚀

A powerful SWR (Stale-While-Revalidate) data fetching library for .NET, inspired by [Vercel SWR](https://swr.vercel.app/) and [TanStack Query](https://tanstack.com/query/latest).

**SwrSharp.Core** is the platform-agnostic core library. Future packages will include:
- `SwrSharp.Blazor` - Blazor Server/WASM integration
- `SwrSharp.Wpf` - WPF integration
- `SwrSharp.Maui` - .NET MAUI integration
- `SwrSharp.Avalonia` - Avalonia UI integration

## ✨ Features

- 🔄 **Automatic Background Refetching** - Keep your data fresh automatically
- 💾 **Smart Caching** - Efficient data caching with configurable stale times
- 🌐 **Network Mode Support** - Handle online/offline scenarios gracefully
- 🔁 **Retry Logic** - Automatic retry with exponential backoff
- ⚡ **Optimistic Updates** - Fast UI updates with background synchronization
- 🎯 **Type-Safe** - Full C# type safety with generics
- 🧵 **Thread-Safe** - Designed for concurrent access
- 📱 **Offline-First Ready** - Support for offline-first architectures
- 🖥️ **Multi-Platform** - Core library works with any .NET UI framework

## 📚 Documentation

- [⚠️ Important Defaults](./21.%20Important%20Defaults.md) - **Start here!** Understand default behaviors
- [🛡️ Security Best Practices](./23.%20Security%20Best%20Practices.md) - **Essential!** Secure your application
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
dotnet add package SwrSharp.Core
```

### Basic Usage

```csharp
using SwrSharp.Core;

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

// Execute and get data
await todosQuery.ExecuteAsync();

if (todosQuery.IsSuccess)
{
    var todos = todosQuery.Data;
    // Use your data!
}
```

### With Type-Safe Default Query Functions

```csharp
using SwrSharp.Core;

// Create query client with default functions per type
var queryClient = new QueryClient();

queryClient.SetDefaultQueryFn<List<Todo>>(async ctx => {
    var response = await httpClient.GetAsync("/api/todos", ctx.Signal);
    return await response.Content.ReadFromJsonAsync<List<Todo>>() 
           ?? new List<Todo>();
});

// Now queries just need a key!
var todosQuery = new UseQuery<List<Todo>>(
    new QueryOptions<List<Todo>>(queryKey: new("todos")),
    queryClient
);

await todosQuery.ExecuteAsync();
```

## 🎯 Key Concepts

### Query States

```csharp
// Status (derived from data/error)
query.IsPending   // No data yet
query.IsSuccess   // Has data
query.IsError     // Has error

// Fetch Status (network activity)
query.IsFetching  // Currently fetching
query.IsLoading   // First load (pending + fetching)
```

### Caching & Freshness

```csharp
new QueryOptions<List<Todo>>(
    queryKey: new("todos"),
    queryFn: async ctx => await FetchTodosAsync(),
    staleTime: TimeSpan.FromMinutes(5) // Data fresh for 5 minutes
)
```

### Network Modes

```csharp
// Online (default): Pause when offline
// Always: Ignore network state  
// OfflineFirst: Try once, then pause if offline
networkMode: NetworkMode.Online
```

### Retry Logic

```csharp
new QueryOptions<List<Todo>>(
    queryKey: new("todos"),
    queryFn: async ctx => await FetchTodosAsync(),
    retry: 3, // Retry 3 times on failure
    retryDelayFunc: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt))
)
```

## 📦 Package Structure

```
SwrSharp.Core        → Core library (you are here)
SwrSharp.Blazor      → Blazor integration (coming soon)
SwrSharp.Wpf         → WPF integration (coming soon)
SwrSharp.Maui        → .NET MAUI integration (coming soon)
SwrSharp.Avalonia    → Avalonia integration (coming soon)
```

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## 📄 License

This project is licensed under the MIT License.

## 🙏 Acknowledgments

- [Vercel SWR](https://swr.vercel.app/) - The original SWR library for React
- [TanStack Query](https://tanstack.com/query/latest) - Powerful data synchronization for web

