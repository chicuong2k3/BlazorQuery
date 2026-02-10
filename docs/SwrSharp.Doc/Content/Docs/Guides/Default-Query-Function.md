---
title: "Default Query Function"
description: "Global default function"
order: 23
category: "Guides"
---

# Default Query Function
SwrSharp allows you to set a default query function that will be used for all queries that don't specify their own.
## Overview
Instead of providing a query function to every `UseQuery` call, you can set a global default that extracts the query key and fetches data automatically.
## Setting Up Default Query Function
### Configure Global Default
```csharp
// In Program.cs
builder.Services.AddSwrSharp(options =>
{
    options.DefaultQueryFn = async (ctx) =>
    {
        var (queryKey, signal) = ctx;
        var url = BuildUrlFromQueryKey(queryKey);
        return await Http.GetFromJsonAsync<object>(url, signal);
    };
});
```
### Using Default Query Function
```csharp
@page "/todos"
@inject QueryClient QueryClient
@implements IAsyncDisposable
@if (Query?.Data is Todo[] todos)
{
    @foreach (var todo in todos)
    {
        <div class="todo">@todo.Title</div>
    }
}
@code {
    private UseQueryResult<Todo[]>? Query;
    protected override async Task OnInitializedAsync()
    {
        // No query function needed - uses default!
        Query = await QueryClient.UseQuery(
            queryKey: new QueryKey("todos")
        );
    }
    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        if (Query != null)
            await Query.DisposeAsync();
    }
}
```
## Query Key Convention
### URL Building from Query Key
```csharp
private static string BuildUrlFromQueryKey(QueryKey key)
{
    // Query key: ["todos"] -> "/api/todos"
    // Query key: ["todos", 1] -> "/api/todos/1"
    // Query key: ["users", 42, "posts"] -> "/api/users/42/posts"
    var parts = key.Parts
        .Select(p => Uri.EscapeDataString(p?.ToString() ?? ""))
        .ToList();
    var path = string.Join("/", parts);
    return $"/api/{path}";
}
```
## Override Default Query Function
### Per-Query Override
You can still override the default for specific queries:
```csharp
@code {
    private UseQueryResult<Todo[]>? Query;
    protected override async Task OnInitializedAsync()
    {
        // Override default for this specific query
        Query = await QueryClient.UseQuery(
            queryKey: new QueryKey("todos"),
            queryFn: async (ctx) =>
            {
                // Custom logic for this query
                var todos = await Http.GetFromJsonAsync<Todo[]>("/api/todos?completed=false", ctx.Signal);
                return todos ?? Array.Empty<Todo>();
            }
        );
    }
}
```
## Advanced Pattern
### Smart URL Building
```csharp
public class SmartDefaultQueryFunction
{
    private readonly HttpClient httpClient;
    public SmartDefaultQueryFunction(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }
    public async Task<object?> FetchAsync(QueryFunctionContext ctx)
    {
        var (queryKey, signal) = ctx;
        // Build URL intelligently
        var url = BuildUrl(queryKey);
        // Determine return type from context
        var responseType = GetResponseType(queryKey);
        // Fetch using reflection or type mapping
        var method = typeof(HttpClientJsonExtensions)
            .GetMethod("GetFromJsonAsync")!
            .MakeGenericMethod(responseType);
        return await (dynamic)method.Invoke(null, new object[] { httpClient, url, signal })!;
    }
    private string BuildUrl(QueryKey key)
    {
        var parts = key.Parts.Select(p => Uri.EscapeDataString(p?.ToString() ?? ""));
        return $"/api/{string.Join("/", parts)}";
    }
    private Type GetResponseType(QueryKey key)
    {
        // Map query key to response type
        return key[0]?.ToString() switch
        {
            "todos" => typeof(Todo[]),
            "users" => typeof(User[]),
            "posts" => typeof(Post[]),
            _ => typeof(object)
        };
    }
}
```
## Benefits
✅ **Less Boilerplate**: No need to define query functions for standard REST APIs  
✅ **Consistency**: All queries follow the same pattern  
✅ **Flexibility**: Can still override per-query when needed  
✅ **Scalability**: Easy to add new endpoints  
## Best Practices
1. **Keep it simple**: Default function should handle common cases
2. **Provide override**: Allow specific queries to use custom logic
3. **Document conventions**: Make URL building rules clear
4. **Handle errors**: Ensure default function handles failures gracefully
5. **Consider types**: Think about how to handle different response types
