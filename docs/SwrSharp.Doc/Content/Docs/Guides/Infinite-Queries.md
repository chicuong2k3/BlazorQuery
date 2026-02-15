---
title: "Infinite Queries"
description: "Building infinite scroll"
order: 14
category: "Guides"
---

Rendering lists that can additively "load more" data onto an existing set of data or "infinite scroll" is a very common UI pattern. SwrSharp supports infinite queries through the `UseInfiniteQuery` class for querying these types of lists.

When using `UseInfiniteQuery`, you'll notice a few things are different:

- `Data` is now an object containing infinite query data:
  - `Data.Pages` - list containing the fetched pages
  - `Data.PageParams` - list containing the page params used to fetch the pages
- `FetchNextPageAsync()` and `FetchPreviousPageAsync()` methods are available
- `InitialPageParam` option is required to specify the initial page param
- `GetNextPageParam` and `GetPreviousPageParam` options determine if there is more data to load
- `HasNextPage` boolean is `true` if `GetNextPageParam` returns non-null
- `HasPreviousPage` boolean is `true` if `GetPreviousPageParam` returns non-null
- `IsFetchingNextPage` and `IsFetchingPreviousPage` booleans distinguish between background refresh and loading more

## Example: Cursor-Based Pagination

Let's assume we have an API that returns pages of projects 3 at a time based on a cursor index:

```
/api/projects?cursor=0  → { data: [...], nextCursor: 3 }
/api/projects?cursor=3  → { data: [...], nextCursor: 6 }
/api/projects?cursor=6  → { data: [...], nextCursor: 9 }
/api/projects?cursor=9  → { data: [...], nextCursor: null }
```

And we want to build an infinite scroll UI that loads more projects when the user clicks a "Load More" button. Here's how we can do that with `UseInfiniteQuery`:

```csharp
public class InfiniteProjectsComponent : IDisposable
{
    private readonly QueryClient _queryClient;
    private UseInfiniteQuery<ProjectsPage, int>? _query;

    public InfiniteProjectsComponent(QueryClient queryClient)
    {
        _queryClient = queryClient;
    }

    public void Initialize()
    {
        _query = new UseInfiniteQuery<ProjectsPage, int>(
            new InfiniteQueryOptions<ProjectsPage, int>(
                queryKey: new("projects"),
                queryFn: async ctx => {
                    // Access pageParam from context (matches React Query behavior)
                    var cursor = (int)ctx.PageParam!;
                    return await FetchProjectsAsync(cursor);
                },
                initialPageParam: 0, // Start from cursor 0 (required)
                getNextPageParam: (lastPage, allPages, lastPageParam) => {
                    // Return next cursor or null if no more
                    return lastPage.NextCursor;
                }
            ),
            _queryClient
        );

        _query.OnChange += () =>
        {
            // Check query state for rendering:
            //
            // _query.IsPending
            //   → Loading first page, show loading screen
            //
            // _query.IsError
            //   → Show error: _query.Error?.Message
            //
            // _query.IsSuccess
            //   → Render all pages from _query.Data.Pages
            //   → If _query.IsFetchingNextPage: show "Loading more..."
            //   → If _query.HasNextPage: show "Load More" button
            //   → If _query.IsFetching && !_query.IsFetchingNextPage: background refresh
            //
            // Notify your UI framework to re-render
        };

        // Load first page
        _ = _query.ExecuteAsync();
    }

    public void LoadMore()
    {
        if (_query == null || !_query.HasNextPage || _query.IsFetching)
            return;

        _ = _query.FetchNextPageAsync();
    }

    public void Dispose()
    {
        _query?.Dispose();
    }
}

// Data models
public class ProjectsPage
{
    public List<Project> Projects { get; set; } = new();
    public int? NextCursor { get; set; }
}

public class Project
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
```

## Key Properties and Methods

### Data Structure

```csharp
// InfiniteData<TData>
query.Data.Pages      // List<TData> - all fetched pages
query.Data.PageParams // List<object?> - params used to fetch each page
```

### Navigation

```csharp
// Fetch next page (fire-and-forget, observe via OnChange)
_ = query.FetchNextPageAsync();

// Fetch previous page
_ = query.FetchPreviousPageAsync();

// Refetch all pages
_ = query.RefetchAsync();
```

### State Flags

```csharp
query.HasNextPage            // bool - can load more forward
query.HasPreviousPage        // bool - can load more backward
query.IsFetchingNextPage     // bool - loading next page
query.IsFetchingPreviousPage // bool - loading previous page
query.IsFetching            // bool - any fetch in progress
```

## Important: Preventing Concurrent Fetches

Calling `FetchNextPageAsync()` while a fetch is in progress can cause data overwrites. Always check `IsFetching`:

```csharp
// ✅ Good: Check before fetching
if (query.HasNextPage && !query.IsFetching)
{
    _ = query.FetchNextPageAsync();
}

// ❌ Bad: No check
_ = query.FetchNextPageAsync(); // Might overwrite data!
```

For infinite scroll:

```csharp
void OnScrollReachedEnd()
{
    // Check both conditions
    if (_query.HasNextPage && !_query.IsFetching)
    {
        _ = _query.FetchNextPageAsync();
    }
}
```

## Refetching Behavior

When an infinite query needs to be refetched (e.g., becomes stale), **each page is fetched sequentially** starting from the first one. This ensures data consistency and avoids duplicates or skipped records due to stale cursors.

```csharp
// User has loaded pages 0, 3, 6
// Data becomes stale, refetch is triggered
_ = query.RefetchAsync();

// Refetches sequentially:
// 1. Page with cursor 0
// 2. Page with cursor 3
// 3. Page with cursor 6
// OnChange fires as each page is refetched
```

## Bi-Directional Infinite Lists

Implement bi-directional scrolling with `GetPreviousPageParam`:

```csharp
var query = new UseInfiniteQuery<ProjectsPage, int>(
    new InfiniteQueryOptions<ProjectsPage, int>(
        queryKey: new("projects"),
        queryFn: async ctx => await FetchProjectsAsync((int)ctx.PageParam!),
        initialPageParam: 0,
        getNextPageParam: (lastPage, allPages, lastPageParam) => 
            lastPage.NextCursor,
        getPreviousPageParam: (firstPage, allPages, firstPageParam) => 
            firstPage.PrevCursor
    ),
    _queryClient
);

// Load newer content
_ = query.FetchNextPageAsync();

// Load older content
_ = query.FetchPreviousPageAsync();
```

## Limiting Pages with `maxPages`

Limit the number of pages kept in memory for performance:

```csharp
var query = new UseInfiniteQuery<ProjectsPage, int>(
    new InfiniteQueryOptions<ProjectsPage, int>(
        queryKey: new("projects"),
        queryFn: async ctx => await FetchProjectsAsync((int)ctx.PageParam!),
        initialPageParam: 0,
        getNextPageParam: (lastPage, allPages, lastPageParam) => 
            lastPage.NextCursor,
        getPreviousPageParam: (firstPage, allPages, firstPageParam) => 
            firstPage.PrevCursor,
        maxPages: 3 // Keep only 3 pages in memory
    ),
    _queryClient
);

// After loading 5 pages, only the latest 3 are kept
// Reduces memory usage
// Reduces refetch time (only 3 pages refetched)
```

## Page Param Calculation

If your API doesn't return a cursor, calculate it from the page param:

```csharp
var query = new UseInfiniteQuery<List<Project>, int>(
    new InfiniteQueryOptions<List<Project>, int>(
        queryKey: new("projects"),
        queryFn: async ctx => 
            await FetchProjectsAsync(page: (int)ctx.PageParam!),
        initialPageParam: 0,
        getNextPageParam: (lastPage, allPages, lastPageParam) => {
            // Return null if no more data
            if (lastPage.Count == 0)
                return null;
            
            // Increment page number
            return lastPageParam + 1;
        },
        getPreviousPageParam: (firstPage, allPages, firstPageParam) => {
            // Return null if at first page
            if (firstPageParam <= 0)
                return null;
            
            // Decrement page number
            return firstPageParam - 1;
        }
    ),
    _queryClient
);
```

## Manual Data Manipulation

Update infinite query data manually:

### Remove First Page

```csharp
var data = query.Data;
data.Pages.RemoveAt(0);
data.PageParams.RemoveAt(0);
```

### Remove Single Item

```csharp
foreach (var page in query.Data.Pages)
{
    page.Projects.RemoveAll(p => p.Id == deletedId);
}
```

### Keep Only First Page

```csharp
var data = query.Data;
data.Pages = data.Pages.Take(1).ToList();
data.PageParams = data.PageParams.Take(1).ToList();
```

**Important**: Always maintain the same structure of `Pages` and `PageParams`!

## Concurrent Fetches with `cancelRefetch`

By default, `FetchNextPageAsync` prevents concurrent fetches. Use `cancelRefetch: false` to allow:

```csharp
// Default: prevents concurrent fetches
_ = query.FetchNextPageAsync(); // If already fetching, does nothing

// Allow concurrent (not recommended)
_ = query.FetchNextPageAsync(cancelRefetch: false);
```

## Best Practices

### 1. **Always Check IsFetching**
```csharp
// ✅ Good
if (query.HasNextPage && !query.IsFetching)
    _ = query.FetchNextPageAsync();

// ❌ Bad
if (query.HasNextPage)
    _ = query.FetchNextPageAsync(); // Can cause overwrites!
```

### 2. **Use MaxPages for Long Lists**
```csharp
// ✅ Good: Limit memory usage
maxPages: 5

// ❌ Bad: Unlimited pages (memory issues)
// No maxPages
```

### 3. **Return null for No More Data**
```csharp
// ✅ Good
getNextPageParam: (lastPage, allPages, lastPageParam) => {
    if (lastPage.Items.Count == 0)
        return null; // No more pages
    return lastPage.NextCursor;
}

// ❌ Bad: Always returns cursor (infinite loop!)
getNextPageParam: (lastPage, allPages, lastPageParam) => 
    lastPageParam + 1 // Never stops!
```

### 4. **Handle Loading States**
```csharp
// ✅ Good: Different states
if (query.IsPending)
    return "Loading first page...";
if (query.IsFetchingNextPage)
    return "Loading more...";
if (query.IsFetching)
    return "Refreshing...";

// ❌ Bad: Generic loading
if (query.IsFetching)
    return "Loading..."; // Can't distinguish states
```
