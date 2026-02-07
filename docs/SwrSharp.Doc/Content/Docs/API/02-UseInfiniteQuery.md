---
title: "UseInfiniteQuery"
description: "API reference for the UseInfiniteQuery hook"
order: 2
category: "API"
---

# UseInfiniteQuery

The `UseInfiniteQuery` hook is used for paginated or infinite scroll queries.

## Usage

```csharp
var query = await QueryClient.UseInfiniteQuery(
    queryKey: new QueryKey("posts"),
    queryFn: FetchPosts,
    options: new InfiniteQueryOptions { /* ... */ }
);
```

## Parameters

### `queryKey` (QueryKey)
A unique identifier for the query.

### `queryFn` (Func<InfiniteQueryFunctionContext, Task<Page<T>>>)
The function that fetches the paginated data. The context includes `pageParam`.

```csharp
async ctx => {
    var (key, pageParam, signal) = ctx;
    return await Http.PostAsJsonAsync("/api/posts/page", new { page = pageParam }, signal);
}
```

### `options` (InfiniteQueryOptions)
Configuration options including `GetNextPageParam` and `GetPreviousPageParam`.

## Return Value

Returns `UseInfiniteQueryResult<T>` with pagination methods:

```csharp
public class UseInfiniteQueryResult<T>
{
    public InfiniteData<T>? Data { get; }              // All fetched pages
    public Task FetchNextPage { get; }                 // Load next page
    public Task FetchPreviousPage { get; }             // Load previous page
    public bool HasNextPage { get; }                   // More pages available?
    public bool HasPreviousPage { get; }               // Previous pages available?
    public bool IsFetchingNextPage { get; }            // Loading next page?
    public bool IsFetchingPreviousPage { get; }        // Loading previous page?
    
    // ... plus all properties from UseQueryResult
}
```

## Example

```csharp
@page "/infinite-posts"
@inject QueryClient QueryClient
@implements IAsyncDisposable

<div class="posts">
    @if (Query?.Data?.Pages != null)
    {
        @foreach (var page in Query.Data.Pages)
        {
            @foreach (var post in page.Items)
            {
                <div class="post-card">@post.Title</div>
            }
        }
    }

    @if (Query?.HasNextPage ?? false)
    {
        <button onclick="@Query.FetchNextPage" disabled="@(Query.IsFetchingNextPage)">
            @if (Query.IsFetchingNextPage) { <span>Loading...</span> }
            else { <span>Load More</span> }
        </button>
    }
</div>

@code {
    private UseInfiniteQueryResult<Post>? Query;

    protected override async Task OnInitializedAsync()
    {
        Query = await QueryClient.UseInfiniteQuery(
            queryKey: new QueryKey("posts"),
            queryFn: FetchPostsPage,
            options: new InfiniteQueryOptions
            {
                GetNextPageParam = (lastPage, allPages) =>
                    lastPage.HasMore ? allPages.Count + 1 : null
            }
        );
    }

    private async Task<Page<Post>> FetchPostsPage(InfiniteQueryFunctionContext ctx)
    {
        var (key, pageParam, signal) = ctx;
        var page = pageParam ?? 1;
        return await Http.GetFromJsonAsync<Page<Post>>($"/api/posts?page={page}", signal)
            ?? new Page<Post>();
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        if (Query != null)
            await Query.DisposeAsync();
    }
}
```

## See Also

- [Infinite Queries Guide](/docs/guides/infinite-queries)
- [Paginated Queries Guide](/docs/guides/paginated-queries)
- [UseQuery](/docs/api/use-query)

