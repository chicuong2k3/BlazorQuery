---
title: "Paginated/Lagged Queries"
description: "Implementing pagination"
order: 13
category: "Guides"
---


Rendering paginated data is a very common UI pattern and in SwrSharp, it "just works" by including the page information in the query key:

```csharp
var page = 1;
var query = new UseQuery<ProjectsPage>(
    new QueryOptions<ProjectsPage>(
        queryKey: new("projects", page),
        queryFn: async ctx => await FetchProjectsAsync(page)
    ),
    queryClient
);
```

However, if you run this simple example, you might notice something strange:

**The UI jumps in and out of the `Success` and `Pending` states because each new page is treated like a brand new query.**

This experience is not optimal and unfortunately is how many tools today insist on working. But not SwrSharp! As you may have guessed, SwrSharp comes with an awesome feature called `placeholderData` that allows us to get around this.

## Better Paginated Queries with `placeholderData`

Consider the following example where we would ideally want to increment a pageIndex (or cursor) for a query. If we were to use `UseQuery` without placeholder data, **it would still technically work fine**, but the UI would jump in and out of the `Success` and `Pending` states as different queries are created and destroyed for each page or cursor. 

By setting `placeholderData` to keep the previous data using `placeholderDataFunc: (previousData, previousQuery) => previousData`, we get a few new things:

- **The data from the last successful fetch is available while new data is being requested, even though the query key has changed**.
- When the new data arrives, the previous `data` is seamlessly swapped to show the new data.
- `IsPlaceholderData` is made available to know what data the query is currently providing you

```csharp
public class PaginatedProjectsComponent : IDisposable
{
    private readonly QueryClient _queryClient;
    private int _currentPage = 0;
    private UseQuery<ProjectsPage>? _projectsQuery;

    public PaginatedProjectsComponent(QueryClient queryClient)
    {
        _queryClient = queryClient;
    }

    public void LoadPage(int page)
    {
        _currentPage = page;
        _projectsQuery?.Dispose();

        _projectsQuery = new UseQuery<ProjectsPage>(
            new QueryOptions<ProjectsPage>(
                queryKey: new("projects", page),
                queryFn: async ctx => await FetchProjectsAsync(page),
                // Keep previous data while loading new page
                placeholderDataFunc: (previousData, previousQuery) => previousData
            ),
            _queryClient
        );

        _projectsQuery.OnChange += () =>
        {
            // Called when state changes (placeholder → real data, or error)
            // Notify your UI framework to re-render
        };

        _ = _projectsQuery.ExecuteAsync();
    }

    // Check query state for rendering:
    //
    // _projectsQuery.IsPending && !_projectsQuery.IsPlaceholderData
    //   → First page load (no previous data), show loading screen
    //
    // _projectsQuery.IsPlaceholderData
    //   → Showing previous page while loading new page
    //
    // _projectsQuery.IsSuccess && !_projectsQuery.IsPlaceholderData
    //   → New page loaded, show real data

    public void PreviousPage()
    {
        if (_currentPage > 0)
        {
            LoadPage(_currentPage - 1);
        }
    }

    public void NextPage()
    {
        // Don't navigate if:
        // 1. Currently showing placeholder data (still loading)
        // 2. No more pages available
        if (_projectsQuery != null &&
            !_projectsQuery.IsPlaceholderData &&
            _projectsQuery.Data?.HasMore == true)
        {
            LoadPage(_currentPage + 1);
        }
    }

    public bool CanGoToPreviousPage => _currentPage > 0;

    public bool CanGoToNextPage =>
        _projectsQuery != null &&
        !_projectsQuery.IsPlaceholderData &&
        _projectsQuery.Data?.HasMore == true;

    public void Dispose()
    {
        _projectsQuery?.Dispose();
    }
}

// Helper classes
public class ProjectsPage
{
    public List<Project> Projects { get; set; } = new();
    public bool HasMore { get; set; }
}

public class Project
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
```

## Key Benefits

### 1. **No Loading Flicker**

Without placeholder data:
```
Page 1 → [Click Next] → Loading spinner → Page 2
         ↑
         Empty screen/loading state
```

With placeholder data:
```
Page 1 → [Click Next] → Page 1 (with loading indicator) → Page 2
                        ↑
                        Still showing content!
```

### 2. **Better UX**

Users can see content continuously without jarring transitions:

```csharp
if (query.IsPlaceholderData)
{
    // Show subtle loading indicator
    Console.WriteLine("🔄 Loading next page...");
    // Content still visible!
}
else
{
    // Show content normally
    Console.WriteLine("Content loaded");
}
```

### 3. **Prevent Double Navigation**

```csharp
// Disable next button while placeholder data is shown
var canGoNext = !query.IsPlaceholderData && query.Data?.HasMore == true;
```

## Simplified Pattern

```csharp
public class SimplePagination : IDisposable
{
    private readonly QueryClient _queryClient;
    private UseQuery<Page>? _pageQuery;
    private int _page = 0;

    public SimplePagination(QueryClient queryClient)
    {
        _queryClient = queryClient;
    }

    public void GoToPage(int page)
    {
        _page = page;
        _pageQuery?.Dispose();

        _pageQuery = new UseQuery<Page>(
            new QueryOptions<Page>(
                queryKey: new("items", page),
                queryFn: async ctx => await FetchPageAsync(page),
                placeholderDataFunc: (prev, _) => prev // Keep previous data
            ),
            _queryClient
        );

        _pageQuery.OnChange += () =>
        {
            // Notify your UI framework to re-render
        };

        _ = _pageQuery.ExecuteAsync();
    }

    public void Dispose() => _pageQuery?.Dispose();
}
```

## Advanced: Cursor-Based Pagination

```csharp
public class CursorPaginatedList
{
    private readonly QueryClient _queryClient;
    private string? _cursor = null;

    public async Task LoadNextAsync()
    {
        var query = new UseQuery<CursorPage>(
            new QueryOptions<CursorPage>(
                queryKey: new("items", _cursor ?? "start"),
                queryFn: async ctx => await FetchWithCursorAsync(_cursor),
                placeholderDataFunc: (prev, _) => prev
            ),
            _queryClient
        );

        await query.ExecuteAsync();

        if (query.IsSuccess && query.Data != null)
        {
            _cursor = query.Data.NextCursor;
            RenderItems(query.Data.Items, query.IsPlaceholderData);
        }
    }

    private void RenderItems(List<string> items, bool isPlaceholder)
    {
        if (isPlaceholder)
            Console.WriteLine("🔄 Loading more...");

        foreach (var item in items)
            Console.WriteLine($"  {item}");
    }
}

public class CursorPage
{
    public List<string> Items { get; set; } = new();
    public string? NextCursor { get; set; }
}
```

## Handling Edge Cases

### 1. **First Page Load**

```csharp
if (query.IsPending && !query.IsPlaceholderData)
{
    // First load - show full loading state
    return LoadingScreen();
}
```

### 2. **Last Page**

```csharp
var isLastPage = query.Data?.HasMore == false;

if (isLastPage)
{
    Console.WriteLine("You've reached the end!");
}
```

### 3. **Empty Pages**

```csharp
if (query.IsSuccess && (query.Data?.Items.Count ?? 0) == 0)
{
    if (page == 0)
        Console.WriteLine("No items found");
    else
        Console.WriteLine("No more items");
}
```

## Best Practices

### 1. **Always Use Placeholder for Pagination**

```csharp
// ✅ Good: Smooth transitions
placeholderDataFunc: (prev, _) => prev

// ❌ Bad: Flickering UI
// No placeholder data
```

### 2. **Disable Next Button During Loading**

```csharp
// ✅ Good: Prevent double-click
var canGoNext = !query.IsPlaceholderData && query.Data?.HasMore == true;

// ❌ Bad: Can navigate while loading
var canGoNext = query.Data?.HasMore == true; // Missing IsPlaceholderData check!
```

### 3. **Show Loading Indicator**

```csharp
// ✅ Good: User knows something is happening
if (query.IsPlaceholderData)
{
    Console.WriteLine("Loading next page...");
    // Still showing content
}

// ❌ Bad: No feedback
// Just showing old content with no indication
```

### 4. **Handle First Load Differently**

```csharp
// ✅ Good: Different UI for first load
if (query.IsPending && !query.IsPlaceholderData)
{
    return FullLoadingScreen(); // First load
}
else if (query.IsPlaceholderData)
{
    return ContentWithSpinner(); // Subsequent loads
}

// ❌ Bad: Same loading state for all
if (query.IsPending)
{
    return LoadingScreen(); // Can't distinguish first vs subsequent
}
```
