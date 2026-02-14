---
title: "Render Optimizations"
description: "Performance optimizations"
order: 20
category: "Guides"
---


# Render Optimizations

SwrSharp provides several strategies to optimize component rendering and reduce unnecessary re-renders.

## Minimize Re-renders

### Use ShouldRender Pattern

```csharp
@code {
    private UseQuery<List<Todo>>? _todosQuery;
    private List<Todo>? _previousData;

    protected override async Task OnInitializedAsync()
    {
        _todosQuery = new UseQuery<List<Todo>>(
            new QueryOptions<List<Todo>>(
                queryKey: new("todos"),
                queryFn: async ctx => await FetchTodosAsync()
            ),
            QueryClient
        );

        _todosQuery.OnChange += StateHasChanged;
        await _todosQuery.ExecuteAsync();
    }

    protected override bool ShouldRender()
    {
        // Only re-render if data actually changed
        if (_todosQuery?.Data == _previousData)
            return false;

        _previousData = _todosQuery?.Data;
        return true;
    }
}
```

## Separate State Management

### Use Multiple Queries

Instead of one large query, split into smaller ones:

```csharp
// Instead of one query with all data
var allDataQuery = new UseQuery<Everything>(
    new QueryOptions<Everything>(
        queryKey: new("everything"),
        queryFn: async ctx => await FetchEverythingAsync()
    ),
    queryClient
);

// Use multiple focused queries
var todosQuery = new UseQuery<List<Todo>>(
    new QueryOptions<List<Todo>>(
        queryKey: new("todos"),
        queryFn: async ctx => await FetchTodosAsync()
    ),
    queryClient
);

var notificationsQuery = new UseQuery<List<Notification>>(
    new QueryOptions<List<Notification>>(
        queryKey: new("notifications"),
        queryFn: async ctx => await FetchNotificationsAsync()
    ),
    queryClient
);

var settingsQuery = new UseQuery<Settings>(
    new QueryOptions<Settings>(
        queryKey: new("settings"),
        queryFn: async ctx => await FetchSettingsAsync()
    ),
    queryClient
);
```

This way, only affected components re-render when their specific data changes.

## Memoization

### Cache Computed Values

```csharp
@code {
    private UseQuery<List<Product>>? _productsQuery;
    private Dictionary<int, int> _categoryCount = new();
    private List<Product>? _previousData;

    protected override bool ShouldRender()
    {
        if (_previousData != _productsQuery?.Data)
        {
            _previousData = _productsQuery?.Data;
            RecomputeCategoryCount();
            return true;
        }
        return false;
    }

    private void RecomputeCategoryCount()
    {
        _categoryCount.Clear();
        if (_productsQuery?.Data != null)
        {
            foreach (var product in _productsQuery.Data)
            {
                if (!_categoryCount.ContainsKey(product.CategoryId))
                    _categoryCount[product.CategoryId] = 0;
                _categoryCount[product.CategoryId]++;
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
@implements IDisposable

<Virtualize Items="_productsQuery?.Data" Context="product">
    <ItemContent>
        <div class="product-row">@product.Name</div>
    </ItemContent>
    <Placeholder>
        <div class="loading-placeholder"></div>
    </Placeholder>
</Virtualize>

@code {
    private UseQuery<List<Product>>? _productsQuery;

    protected override async Task OnInitializedAsync()
    {
        _productsQuery = new UseQuery<List<Product>>(
            new QueryOptions<List<Product>>(
                queryKey: new("products"),
                queryFn: async ctx => {
                    return await Http.GetFromJsonAsync<List<Product>>(
                        "/api/products", ctx.Signal
                    ) ?? new List<Product>();
                }
            ),
            QueryClient
        );

        _productsQuery.OnChange += StateHasChanged;
        await _productsQuery.ExecuteAsync();
    }

    public void Dispose()
    {
        if (_productsQuery != null)
        {
            _productsQuery.OnChange -= StateHasChanged;
            _productsQuery.Dispose();
        }
    }
}
```

## Query Options for Performance

### Increase Stale Time

```csharp
var query = new UseQuery<List<Product>>(
    new QueryOptions<List<Product>>(
        queryKey: new("products"),
        queryFn: async ctx => await FetchProductsAsync(),
        // Data stays fresh longer, reducing refetches
        staleTime: TimeSpan.FromMinutes(5),
        // Don't refetch on window focus if fresh
        refetchOnWindowFocus: false
    ),
    queryClient
);
```

### Disable Unnecessary Refetching

```csharp
var query = new UseQuery<List<Product>>(
    new QueryOptions<List<Product>>(
        queryKey: new("products"),
        queryFn: async ctx => await FetchProductsAsync(),
        // No automatic polling
        refetchInterval: null,
        // No refetch on reconnect
        refetchOnReconnect: false,
        // No refetch on window focus
        refetchOnWindowFocus: false
    ),
    queryClient
);
```

## Best Practices

1. **Split queries**: Use multiple focused queries instead of one large query
2. **Control stale time**: Increase stale time to reduce unnecessary refetches
3. **Use virtualization**: For large lists, use virtual scrolling
4. **Memoize computations**: Cache expensive calculations
5. **Monitor performance**: Use browser DevTools to identify bottlenecks
6. **Lazy load**: Load data only when needed
