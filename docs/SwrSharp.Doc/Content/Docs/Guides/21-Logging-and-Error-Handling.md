---
title: "Logging and Error Handling"
description: "Guide for Logging and Error Handling in SwrSharp"
order: 21
category: "Guides"
---
# Logging and Error Handling
Proper logging and error handling are crucial for debugging and maintaining production applications.
## Error Handling
### Accessing Error Information
```csharp
@page "/todos"
@inject QueryClient QueryClient
@implements IAsyncDisposable
@if (Query?.IsError ?? false)
{
    <div class="error-alert">
        <h3>Error loading todos</h3>
        <p>@Query.Error?.Message</p>
        @if (Query.FailureReason != null)
        {
            <details>
                <summary>Details</summary>
                <pre>@Query.FailureReason.StackTrace</pre>
            </details>
        }
        <button @onclick="RetryFetch">Retry</button>
    </div>
}
else if (Query?.Data != null)
{
    @foreach (var todo in Query.Data)
    {
        <div class="todo">@todo.Title</div>
    }
}
@code {
    private UseQueryResult<Todo[]>? Query;
    protected override async Task OnInitializedAsync()
    {
        Query = await QueryClient.UseQuery(
            queryKey: new QueryKey("todos"),
            queryFn: FetchTodos
        );
    }
    private async Task<Todo[]> FetchTodos(QueryFunctionContext ctx)
    {
        try
        {
            return await Http.GetFromJsonAsync<Todo[]>("/api/todos", ctx.Signal) 
                ?? Array.Empty<Todo>();
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException("Failed to fetch todos", ex);
        }
    }
    private async Task RetryFetch()
    {
        await QueryClient.InvalidateQueries(new QueryKey("todos"));
    }
    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        if (Query != null)
            await Query.DisposeAsync();
    }
}
```
## Best Practices
1. Always handle errors in production
2. Log with context: include query keys
3. Use error boundaries
4. Track failures
5. Implement retry UI
6. Show user-friendly messages
