---
title: "Scroll Restoration"
description: "Restoring scroll position"
order: 19
category: "Guides"
---


# Scroll Restoration

Scroll restoration automatically saves and restores the scroll position when navigating between pages.

## Overview

When users navigate to a new page and then return using the browser back button, SwrSharp can restore the scroll position to where they were previously.

## Configuration

### Enable Scroll Restoration

```csharp
new QueryOptions
{
    RestoreScroll = true
}
```

### Scroll Position Recovery

```csharp
@page "/products"
@inject QueryClient QueryClient
@implements IAsyncDisposable

<div @ref="scrollContainer" class="overflow-y-auto h-screen">
    @if (Products?.Data != null)
    {
        @foreach (var product in Products.Data)
        {
            <div class="product-item">@product.Name</div>
        }
    }
</div>

@code {
    private ElementReference scrollContainer;
    private UseQueryResult<Product[]>? Products;

    protected override async Task OnInitializedAsync()
    {
        Products = await QueryClient.UseQuery(
            queryKey: new QueryKey("products"),
            queryFn: FetchProducts,
            options: new QueryOptions 
            { 
                RestoreScroll = true 
            }
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

## Best Practices

1. **Use with virtualization**: Combine scroll restoration with virtual scrolling for better performance
2. **Clear when needed**: Reset scroll position when data changes significantly
3. **Test navigation**: Verify scroll position works with back/forward buttons
4. **Consider mobile**: Test on mobile devices where scroll behavior differs

## Limitations

- Only works with browser back/forward navigation
- May not work with all client-side routing scenarios
- Requires explicit component implementation
