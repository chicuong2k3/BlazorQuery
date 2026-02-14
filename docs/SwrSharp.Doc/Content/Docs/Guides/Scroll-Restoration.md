---
title: "Scroll Restoration"
description: "Restoring scroll position"
order: 19
category: "Guides"
---


# Scroll Restoration

> **Note**: Scroll restoration is NOT a built-in feature of SwrSharp. Unlike React Query which works in the browser with built-in scroll restoration APIs, SwrSharp is a platform-agnostic .NET library. This guide provides patterns for implementing scroll restoration manually in your application.

## Overview

When users navigate to a new page and then return, they expect to see the scroll position where they were previously. Since SwrSharp caches query data, the data is instantly available on return - but you still need to restore the scroll position manually.

## Blazor Implementation Pattern

### Save and Restore Scroll Position

```csharp
@page "/products"
@inject QueryClient QueryClient
@inject IJSRuntime JS
@implements IDisposable

<div @ref="_scrollContainer" class="overflow-y-auto h-screen">
    @if (_productsQuery?.Data != null)
    {
        @foreach (var product in _productsQuery.Data)
        {
            <div class="product-item">@product.Name</div>
        }
    }
</div>

@code {
    private ElementReference _scrollContainer;
    private UseQuery<List<Product>>? _productsQuery;
    private static readonly Dictionary<string, double> _scrollPositions = new();

    protected override async Task OnInitializedAsync()
    {
        _productsQuery = new UseQuery<List<Product>>(
            new QueryOptions<List<Product>>(
                queryKey: new("products"),
                queryFn: async ctx => {
                    return await Http.GetFromJsonAsync<List<Product>>(
                        "/api/products", ctx.Signal
                    ) ?? new List<Product>();
                },
                staleTime: TimeSpan.FromMinutes(5)
            ),
            QueryClient
        );

        _productsQuery.OnChange += StateHasChanged;
        await _productsQuery.ExecuteAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && _scrollPositions.TryGetValue("products", out var scrollTop))
        {
            // Restore scroll position
            await JS.InvokeVoidAsync("setScrollTop", _scrollContainer, scrollTop);
        }
    }

    private async Task SaveScrollPosition()
    {
        var scrollTop = await JS.InvokeAsync<double>("getScrollTop", _scrollContainer);
        _scrollPositions["products"] = scrollTop;
    }

    public void Dispose()
    {
        // Save scroll position before disposing
        _ = SaveScrollPosition();

        if (_productsQuery != null)
        {
            _productsQuery.OnChange -= StateHasChanged;
            _productsQuery.Dispose();
        }
    }
}
```

**JavaScript helper (wwwroot/js/scroll.js)**:
```javascript
window.getScrollTop = (element) => element?.scrollTop ?? 0;
window.setScrollTop = (element, value) => {
    if (element) element.scrollTop = value;
};
```

## Best Practices

1. **Use with caching**: Combine scroll restoration with `staleTime` so data is instantly available on return
2. **Clear when needed**: Reset scroll position when data changes significantly
3. **Test navigation**: Verify scroll position works with back/forward buttons
4. **Consider mobile**: Test on mobile devices where scroll behavior differs

## Limitations

- Requires manual implementation per component
- Only works with explicit scroll position tracking
- May not work with all client-side routing scenarios
- Virtual scrolling requires additional handling
