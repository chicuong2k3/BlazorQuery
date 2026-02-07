---
title: "Render Optimizations"
description: "Guide for Render Optimizations in SwrSharp"
order: 20
category: "Guides"
---

# Render Optimizations

SwrSharp provides several strategies to optimize component rendering and reduce unnecessary re-renders.

## Minimize Re-renders

### Use Shouldupdate Pattern

```csharp
@code {
    private UseQueryResult<Todo[]>? Query;
    private Todo[]? previousData;

    protected override async Task OnInitializedAsync()
    {
        Query = await QueryClient.UseQuery(
            queryKey: new QueryKey("todos"),
            queryFn: FetchTodos
        );
    }

    protected override bool ShouldRender()
    {
        // Only re-render if data actually changed
        if (Query?.Data == previousData)
            return false;

        previousData = Query?.Data;
        return true;
    }
}
```

## Separate State Management

### Use Multiple Queries

Instead of one large query, split into smaller ones:

```csharp
// Instead of one query with all data
var allData = await QueryClient.UseQuery(
    new QueryKey("everything"),
    FetchEverything
);

// Use multiple focused queries
var todos = await QueryClient.UseQuery(
    new QueryKey("todos"),
    FetchTodos
);

var notifications = await QueryClient.UseQuery(
    new QueryKey("notifications"),
    FetchNotifications
);

var settings = await QueryClient.UseQuery(
    new QueryKey("settings"),
    FetchSettings
);
```

This way, only affected components re-render when their specific data changes.

## Memoization

### Cache Computed Values

```csharp
@code {
    private UseQueryResult<Product[]>? Products;
    private Dictionary<int, int> categoryCount = new();
    private string[] previousData;

    protected override bool ShouldRender()
    {
        if (previousData != Products?.Data)
        {
            previousData = Products?.Data;
            RecomputeCategoryCount();
            return true;
        }
        return false;
    }

    private void RecomputeCategoryCount()
    {
        categoryCount.Clear();
        if (Products?.Data != null)
        {
            foreach (var product in Products.Data)
            {
                if (!categoryCount.ContainsKey(product.CategoryId))
                    categoryCount[product.CategoryId] = 0;
                categoryCount[product.CategoryId]++;
            }
        }
    }
}
```

## Large List Virtualization

### Use Virtual Scrolling

```csharp
@page "/large-list"
@inject QueryClient QueryClient
@implements IAsyncDisposable

<Virtualize Items="Products?.Data?.ToList()" Context="product">
    <ItemContent>
        <div class="product-row">@product.Name</div>
    </ItemContent>
    <Placeholder>
        <div class="loading-placeholder"></div>
    </Placeholder>
</Virtualize>

@code {
    private UseQueryResult<Product[]>? Products;

    protected override async Task OnInitializedAsync()
    {
        Products = await QueryClient.UseQuery(
            queryKey: new QueryKey("products"),
            queryFn: FetchProducts
        );
    }

    private async Task<Product[]> FetchProducts(QueryFunctionContext ctx)
    {
        return await Http.GetFromJsonAsync<Product[]>("/api/products", ctx.Signal) 
            ?? Array.Empty<Product>();
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        if (Products != null)
            await Products.DisposeAsync();
    }
}
```

## Query Options for Performance

### Increase Stale Time

```csharp
new QueryOptions
{
    // Data stays fresh longer, reducing refetches
    StaleTime = TimeSpan.FromMinutes(5),
    
    // Don't refetch on mount if data is fresh
    RefetchOnMount = false,
    
    // Don't refetch on window focus if fresh
    RefetchOnWindowFocus = false
}
```

### Disable Unnecessary Refetching

```csharp
new QueryOptions
{
    // No automatic refetching
    RefetchInterval = null,
    RefetchIntervalInBackground = false,
    RefetchOnReconnect = false,
    RefetchOnWindowFocus = false
}
```

## Best Practices

1. **Split queries**: Use multiple focused queries instead of one large query
2. **Control stale time**: Increase stale time to reduce unnecessary refetches
3. **Use virtualization**: For large lists, use virtual scrolling
4. **Memoize computations**: Cache expensive calculations
5. **Monitor performance**: Use browser DevTools to identify bottlenecks
6. **Lazy load**: Load data only when needed

