---
title: "Logging and Error Handling"
description: "Logging best practices"
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
@implements IDisposable

@if (_todosQuery?.IsError ?? false)
{
    <div class="error-alert">
        <h3>Error loading todos</h3>
        <p>@_todosQuery.Error?.Message</p>
        @if (_todosQuery.FailureReason != null)
        {
            <details>
                <summary>Details</summary>
                <pre>@_todosQuery.FailureReason.StackTrace</pre>
            </details>
        }
        <button @onclick="RetryFetchAsync">Retry</button>
    </div>
}
else if (_todosQuery?.Data != null)
{
    @foreach (var todo in _todosQuery.Data)
    {
        <div class="todo">@todo.Title</div>
    }
}

@code {
    private UseQuery<List<Todo>>? _todosQuery;

    protected override async Task OnInitializedAsync()
    {
        _todosQuery = new UseQuery<List<Todo>>(
            new QueryOptions<List<Todo>>(
                queryKey: new("todos"),
                queryFn: async ctx => {
                    try
                    {
                        return await Http.GetFromJsonAsync<List<Todo>>(
                            "/api/todos", ctx.Signal
                        ) ?? new List<Todo>();
                    }
                    catch (HttpRequestException ex)
                    {
                        throw new InvalidOperationException("Failed to fetch todos", ex);
                    }
                }
            ),
            QueryClient
        );

        _todosQuery.OnChange += StateHasChanged;
        await _todosQuery.ExecuteAsync();
    }

    private async Task RetryFetchAsync()
    {
        await _todosQuery!.RefetchAsync();
    }

    public void Dispose()
    {
        if (_todosQuery != null)
        {
            _todosQuery.OnChange -= StateHasChanged;
            _todosQuery.Dispose();
        }
    }
}
```

## Using IQueryLogger

SwrSharp provides a logging interface `IQueryLogger` that you can implement for diagnostics:

```csharp
// Use ConsoleQueryLogger for development
var queryClient = new QueryClient(
    logger: new ConsoleQueryLogger()
);

// Or implement your own
public class SerilogQueryLogger : IQueryLogger
{
    private readonly ILogger _logger;

    public SerilogQueryLogger(ILogger logger) => _logger = logger;

    public void LogDebug(string message, params object?[] args) =>
        _logger.Debug(message, args);

    public void LogInformation(string message, params object?[] args) =>
        _logger.Information(message, args);

    public void LogWarning(string message, params object?[] args) =>
        _logger.Warning(message, args);

    public void LogError(Exception? exception, string message, params object?[] args) =>
        _logger.Error(exception, message, args);

    public void LogCritical(Exception? exception, string message, params object?[] args) =>
        _logger.Fatal(exception, message, args);
}
```

## Monitoring Retry Progress

```csharp
var query = new UseQuery<List<Todo>>(
    new QueryOptions<List<Todo>>(
        queryKey: new("todos"),
        queryFn: async ctx => await FetchTodosAsync(ctx.Signal),
        retry: 3
    ),
    queryClient
);

query.OnChange += () => {
    if (query.FailureCount > 0 && query.Error == null)
    {
        // Still retrying
        Console.WriteLine($"Attempt {query.FailureCount} failed: {query.FailureReason?.Message}");
    }
    else if (query.Error != null)
    {
        // All retries exhausted
        Console.WriteLine($"Failed after {query.FailureCount} attempts: {query.Error.Message}");
    }
};
```

## Best Practices
1. Always handle errors in production
2. Log with context: include query keys
3. Use error boundaries
4. Track failures with `FailureCount` and `FailureReason`
5. Implement retry UI with `RefetchAsync()`
6. Show user-friendly messages, hide technical details
