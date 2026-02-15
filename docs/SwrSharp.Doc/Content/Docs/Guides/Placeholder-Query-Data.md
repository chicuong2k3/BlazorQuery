---
title: "Placeholder Query Data"
description: "Using placeholder data"
order: 12
category: "Guides"
---

## What is Placeholder Data?

Placeholder data allows a query to behave as if it already has data, similar to `initialData`, but **the data is NOT persisted to the cache**. This is useful for partial or preview data while the actual data is fetched in the background.

**Key Differences**:
- ✅ `initialData`: Persisted to cache, treated as real data
- ✅ `placeholderData`: NOT persisted, temporary display only

**Example Use Case**: A blog post list has preview data (title + snippet). When viewing an individual post, use the preview as placeholder while fetching the full content.

## Basic Usage

### Placeholder Data as a Value

```csharp
var placeholderTodos = new List<Todo>
{
    new() { Id = 1, Title = "Loading..." },
    new() { Id = 2, Title = "Please wait..." }
};

var query = new UseQuery<List<Todo>>(
    new QueryOptions<List<Todo>>(
        queryKey: new("todos"),
        queryFn: async ctx => await FetchTodosAsync(),
        placeholderData: placeholderTodos
    ),
    queryClient
);

// Immediate state (before ExecuteAsync):
// - Data = placeholderTodos
// - Status = Success (has data to display)
// - IsPlaceholderData = true
// - Cache is EMPTY (not persisted)

query.OnChange += () =>
{
    if (!query.IsPlaceholderData && query.IsSuccess)
    {
        // Real data fetched — Data = real todos, IsPlaceholderData = false
        // Notify your UI framework to re-render
    }
};

_ = query.ExecuteAsync();
```

## `IsPlaceholderData` Flag

When using placeholder data, the query starts in `Success` state because it has data to display. Use `IsPlaceholderData` to distinguish placeholder from real data:

```csharp
var query = new UseQuery<Data>(
    new QueryOptions<Data>(
        queryKey: new("data"),
        queryFn: async ctx => await FetchDataAsync(),
        placeholderData: previewData
    ),
    queryClient
);

// Immediately after construction: IsPlaceholderData = true
// Can render placeholder UI right away

query.OnChange += () =>
{
    if (query.IsPlaceholderData)
    {
        // Still showing placeholder (fetching in progress)
    }
    else if (query.IsSuccess)
    {
        // Real data available
    }

    // Notify your UI framework to re-render
};

_ = query.ExecuteAsync();
```

## Placeholder Data as a Function

Use `placeholderDataFunc` to compute placeholder data, with access to `previousData` and `previousQuery` for smooth transitions:

```csharp
var query = new UseQuery<Data>(
    new QueryOptions<Data>(
        queryKey: new("data", id),
        queryFn: async ctx => await FetchDataAsync(id),
        placeholderDataFunc: (previousData, previousQuery) => {
            // Keep showing old data while fetching new data
            // Useful for paginated queries
            return previousData;
        }
    ),
    queryClient
);

// When ID changes: shows old data as placeholder, then updates to new data
```

## Placeholder Data from Cache

Get placeholder data from another query's cache (e.g., list → detail):

```csharp
// Assume a blog posts list query has already been executed and cached.
// When navigating to a post detail, use preview from list cache as placeholder:

var postId = 123;
var postQuery = new UseQuery<BlogPost>(
    new QueryOptions<BlogPost>(
        queryKey: new("blogPost", postId),
        queryFn: async ctx => await FetchFullBlogPostAsync(postId),
        // Returns: { Id, Title, FullContent } - complete data
        placeholderDataFunc: (_, __) => {
            // Use preview from list as placeholder
            var posts = queryClient.GetQueryData<List<BlogPost>>(new("blogPosts"));
            return posts?.Find(p => p.Id == postId);
        }
    ),
    queryClient
);

// Immediate (if preview found in cache):
// - Shows preview (title + snippet)
// - IsPlaceholderData = true
// - Status = Success (can render UI)

postQuery.OnChange += () =>
{
    if (!postQuery.IsPlaceholderData && postQuery.IsSuccess)
    {
        // Full content loaded from server
    }

    // Notify your UI framework to re-render
};

_ = postQuery.ExecuteAsync();

// After fetch completes (via OnChange):
// - Shows full content
// - IsPlaceholderData = false
```

## Conditional Placeholder Data

Only use cached data as placeholder if it's fresh enough:

```csharp
var query = new UseQuery<Post>(
    new QueryOptions<Post>(
        queryKey: new("post", postId),
        queryFn: async ctx => await FetchPostAsync(postId),
        placeholderDataFunc: (_, __) => {
            var state = queryClient.GetQueryState(new("posts"));
            
            // Only use as placeholder if < 10 seconds old
            if (state != null && 
                (DateTime.UtcNow - state.DataUpdatedAt).TotalSeconds < 10)
            {
                var posts = state.Data as List<Post>;
                return posts?.Find(p => p.Id == postId);
            }
            
            return null; // Too old, don't use as placeholder
        }
    ),
    queryClient
);
```

## Priority: Initial Data vs Placeholder Data

If both are provided, `initialData` takes priority:

```csharp
var query = new UseQuery<string>(
    new QueryOptions<string>(
        queryKey: new("data"),
        queryFn: async ctx => await FetchAsync(),
        initialData: "Initial",       // Priority 1: Used, persisted to cache
        placeholderData: "Placeholder" // Priority 2: Ignored
    ),
    queryClient
);

// Uses initial data (not placeholder)
// IsPlaceholderData = false
```

## Paginated Queries with Placeholder

Keep showing old page while fetching new page:

```csharp
public class PaginatedPosts : IDisposable
{
    private readonly QueryClient _queryClient;
    private UseQuery<List<Post>>? _postsQuery;

    public PaginatedPosts(QueryClient queryClient)
    {
        _queryClient = queryClient;
    }

    public void LoadPage(int page)
    {
        _postsQuery?.Dispose();

        _postsQuery = new UseQuery<List<Post>>(
            new QueryOptions<List<Post>>(
                queryKey: new("posts", page),
                queryFn: async ctx => await FetchPostsPageAsync(page),
                placeholderDataFunc: (previousData, previousQuery) =>
                {
                    // Keep showing old page data while fetching new page
                    // Prevents flickering/loading state
                    return previousData;
                }
            ),
            _queryClient
        );

        _postsQuery.OnChange += () =>
        {
            if (_postsQuery.IsPlaceholderData)
            {
                // Showing previous page data while loading new page
            }
            else if (_postsQuery.IsSuccess)
            {
                // New page loaded
            }

            // Notify your UI framework to re-render
        };

        _ = _postsQuery.ExecuteAsync();
    }

    public void Dispose()
    {
        _postsQuery?.Dispose();
    }
}
```

## Best Practices

### 1. **Use for Partial/Preview Data**

```csharp
// ✅ Good: Preview while fetching full content
placeholderData: new BlogPost 
{ 
    Title = "Post Title", 
    Snippet = "Preview..." 
}

// ❌ Bad: Complete data (use initialData instead)
placeholderData: completeDataObject
```

### 2. **Don't Persist Placeholder Data**

```csharp
// ✅ Good: Placeholder NOT in cache
var query = new UseQuery<Data>(
    new QueryOptions<Data>(
        queryKey: new("data"),
        queryFn: async ctx => await FetchAsync(),
        placeholderData: previewData
    ),
    queryClient
);

// Verify cache is empty
var cached = queryClient.GetQueryData<Data>(new("data"));
Assert.Null(cached); // ✓ Placeholder not persisted
```

### 3. **Check `IsPlaceholderData` in UI**

```csharp
// ✅ Good: Distinguish placeholder from real data
if (query.IsPlaceholderData)
{
    return PreviewComponent(query.Data);
}
else
{
    return FullComponent(query.Data);
}

// ❌ Bad: Treating placeholder as real data
return FullComponent(query.Data); // Might be incomplete!
```

### 4. **Use Function for Expensive Computations**

```csharp
// ✅ Good: Lazy evaluation
placeholderDataFunc: (prev, prevQuery) => {
    return ExpensiveComputation(); // Only called once
}

// ❌ Bad: Computed immediately
placeholderData: ExpensiveComputation() // Called on every render
```
