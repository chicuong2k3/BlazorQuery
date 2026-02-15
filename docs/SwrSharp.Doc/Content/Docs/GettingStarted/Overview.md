---
title: "Overview"
description: "Introduction to SwrSharp"
order: 1
category: "Getting Started"
---


# Overview

SwrSharp is a powerful data fetching and caching library for Blazor applications, inspired by [TanStack Query](https://tanstack.com/query/latest). It provides:

- **Intelligent Caching**: Automatic caching and invalidation of server data
- **Background Refetching**: Keep your data fresh without blocking user interactions
- **Network Status Handling**: Smart handling of online/offline scenarios
- **Retry Logic**: Configurable retry strategies with exponential backoff
- **State Management**: Comprehensive query state including loading, error, and success states

## Key Features

### 🚀 Performance
- Efficient caching reduces unnecessary network requests
- Background refetching keeps data fresh without blocking UI
- Configurable stale time and cache invalidation strategies

### 🔄 Reliability
- Intelligent retry logic with exponential backoff
- Network mode support (online, offline, always)
- Graceful error handling and recovery

### 🎯 Developer Experience
- Type-safe API
- Intuitive hooks: `UseQuery`, `UseMutation`, `UseInfiniteQuery`
- Detailed documentation and examples
- Strong TypeScript-like typing in C#

## Quick Example

```csharp
@page "/todos"
@inject QueryClient QueryClient
@implements IAsyncDisposable

<div>
    @if (Query.IsLoading)
    {
        <p>Loading...</p>
    }
    else if (Query.IsError)
    {
        <p>Error: @Query.Error?.Message</p>
    }
    else
    {
        @foreach (var todo in Query.Data ?? Array.Empty<Todo>())
        {
            <p>@todo.Title</p>
        }
    }
</div>

@code {
    private UseQueryResult<Todo[]> Query = null!;

    protected override async Task OnInitializedAsync()
    {
        Query = await QueryClient.UseQuery(
            queryKey: new QueryKey("todos"),
            queryFn: async ctx => await FetchTodos(ctx.Signal)
        );
    }

    private async Task<Todo[]> FetchTodos(CancellationToken cancellationToken)
    {
        // Fetch from your API
        return await Http.GetFromJsonAsync<Todo[]>("/api/todos", cancellationToken) 
            ?? Array.Empty<Todo>();
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        if (Query != null)
            await Query.DisposeAsync();
    }
}
```

## Next Steps

- Learn about [Query Keys](/docs/guides/query-keys)
- Understand [Query Functions](/docs/guides/query-functions)
- Explore [Network Modes](/docs/guides/network-mode)
