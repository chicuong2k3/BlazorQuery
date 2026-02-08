---
title: "Paginated Queries"
description: "Implementing pagination"
order: 13
category: "Guides"
---

# Paginated / Lagged Queries

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

## Complete Example: Paginated Projects

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

    public async Task LoadPageAsync(int page)
    {
        _currentPage = page;

        // Create new query for this page
        _projectsQuery = new UseQuery<ProjectsPage>(
            new QueryOptions<ProjectsPage>(
                queryKey: new("projects", page),
                queryFn: async ctx => {
                    Console.WriteLine($"Fetching page {page}...");
                    return await FetchProjectsAsync(page);
                },
                // Keep previous data while loading new page
                placeholderDataFunc: (previousData, previousQuery) => previousData
            ),
            _queryClient
        );

        // Subscribe to changes
        _projectsQuery.OnChange += RenderUI;

        // Render with previous data if available (placeholder)
        RenderUI();

        // Fetch new page
        await _projectsQuery.ExecuteAsync();

        // Render with new data
        RenderUI();
    }

    private void RenderUI()
    {
        if (_projectsQuery == null)
            return;

        Console.WriteLine("========================");
        Console.WriteLine($"Page: {_currentPage + 1}");
        Console.WriteLine("========================");

        if (_projectsQuery.IsPending && !_projectsQuery.IsPlaceholderData)
        {
            // First page load (no previous data)
            Console.WriteLine("⏳ Loading first page...");
        }
        else if (_projectsQuery.IsError)
        {
            Console.WriteLine($"❌ Error: {_projectsQuery.Error?.Message}");
        }
        else
        {
            // Has data (real or placeholder)
            var data = _projectsQuery.Data!;

            if (_projectsQuery.IsPlaceholderData)
            {
                Console.WriteLine("📄 [Showing previous page while loading...]");
            }

            Console.WriteLine($"Projects on page {_currentPage + 1}:");
            foreach (var project in data.Projects)
            {
                Console.WriteLine($"  - {project.Name}");
            }

            Console.WriteLine();
            Console.WriteLine($"Has more: {data.HasMore}");
        }

        if (_projectsQuery.IsFetching)
        {
            Console.WriteLine("🔄 Loading...");
        }

        Console.WriteLine("========================");
    }

    public async Task PreviousPageAsync()
    {
        if (_currentPage > 0)
        {
            await LoadPageAsync(_currentPage - 1);
        }
    }

    public async Task NextPageAsync()
    {
        // Don't navigate if:
        // 1. Currently showing placeholder data (still loading)
        // 2. No more pages available
        if (_projectsQuery != null && 
            !_projectsQuery.IsPlaceholderData && 
            _projectsQuery.Data?.HasMore == true)
        {
            await LoadPageAsync(_currentPage + 1);
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
        _queryClient?.Dispose();
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

## Usage Example

```csharp
var component = new PaginatedProjectsComponent(queryClient);

// Load first page
await component.LoadPageAsync(0);

// Output:
// ========================
// Page: 1
// ========================
// ⏳ Loading first page...
// ========================
//
// (after fetch completes)
// ========================
// Page: 1
// ========================
// Projects on page 1:
//   - Project A
//   - Project B
//   - Project C
// Has more: true
// ========================

// Go to next page
await component.NextPageAsync();

// Output:
// ========================
// Page: 2
// ========================
// 📄 [Showing previous page while loading...]
// Projects on page 1:      ← Still showing page 1 data
//   - Project A
//   - Project B
//   - Project C
// Has more: true
// 🔄 Loading...
// ========================
//
// (after fetch completes)
// ========================
// Page: 2
// ========================
// Projects on page 2:      ← Now showing page 2 data
//   - Project D
//   - Project E
//   - Project F
// Has more: true
// ========================
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

if (canGoNext)
{
    await LoadNextPageAsync();
}
else
{
    Console.WriteLine("Please wait for current page to load");
}
```

## Simplified Pattern with Helper Method

```csharp
public class SimplePagination
{
    private readonly QueryClient _queryClient;
    private int _page = 0;

    public async Task<UseQuery<Page>> CreatePageQueryAsync(int page)
    {
        var query = new UseQuery<Page>(
            new QueryOptions<Page>(
                queryKey: new("items", page),
                queryFn: async ctx => await FetchPageAsync(page),
                placeholderDataFunc: (prev, _) => prev // Keep previous data
            ),
            _queryClient
        );

        await query.ExecuteAsync();
        return query;
    }

    public async Task GoToPageAsync(int page)
    {
        _page = page;
        var query = await CreatePageQueryAsync(page);
        RenderPage(query);
    }

    private void RenderPage(UseQuery<Page> query)
    {
        if (query.IsPlaceholderData)
        {
            Console.WriteLine("[Loading new page...]");
        }

        foreach (var item in query.Data?.Items ?? new())
        {
            Console.WriteLine($"- {item}");
        }
    }
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

## Comparison with React Query

### React Query (TypeScript):
```typescript
import { keepPreviousData, useQuery } from '@tanstack/react-query'

function Projects() {
  const [page, setPage] = useState(0)

  const { data, isPending, isPlaceholderData } = useQuery({
    queryKey: ['projects', page],
    queryFn: () => fetchProjects(page),
    placeholderData: keepPreviousData,
  })

  return (
    <div>
      {isPending ? (
        <div>Loading...</div>
      ) : (
        <div>
          {data.projects.map(p => <div key={p.id}>{p.name}</div>)}
        </div>
      )}
      <button onClick={() => setPage(p => p - 1)} disabled={page === 0}>
        Previous
      </button>
      <button 
        onClick={() => setPage(p => p + 1)}
        disabled={isPlaceholderData || !data.hasMore}
      >
        Next
      </button>
    </div>
  )
}
```

### SwrSharp (C#):
```csharp
public class ProjectsComponent
{
    private int _page = 0;
    private UseQuery<ProjectsPage>? _query;

    public async Task LoadPageAsync(int page)
    {
        _page = page;

        _query = new UseQuery<ProjectsPage>(
            new QueryOptions<ProjectsPage>(
                queryKey: new("projects", page),
                queryFn: async ctx => await FetchProjectsAsync(page),
                placeholderDataFunc: (prev, _) => prev // Like keepPreviousData
            ),
            _queryClient
        );

        await _query.ExecuteAsync();
        RenderUI();
    }

    private void RenderUI()
    {
        if (_query == null) return;

        if (_query.IsPending && !_query.IsPlaceholderData)
        {
            Console.WriteLine("Loading...");
        }
        else if (_query.Data != null)
        {
            foreach (var project in _query.Data.Projects)
                Console.WriteLine(project.Name);
        }

        // Previous button
        var canPrev = _page > 0;
        // Next button
        var canNext = !_query.IsPlaceholderData && _query.Data?.HasMore == true;
    }
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
