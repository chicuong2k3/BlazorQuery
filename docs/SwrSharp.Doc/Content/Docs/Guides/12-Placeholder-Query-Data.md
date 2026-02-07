---
title: "Placeholder Query Data"
description: "Guide for Placeholder Query Data in SwrSharp"
order: 12
category: "Guides"
---
# Placeholder Query Data

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

// Immediate state:
// - Data = placeholderTodos
// - Status = Success (has data to display)
// - IsPlaceholderData = true
// - Cache is EMPTY (not persisted)

await query.ExecuteAsync();

// After fetch:
// - Data = real todos
// - IsPlaceholderData = false
// - Cache has real data
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

if (query.IsPlaceholderData)
{
    Console.WriteLine("Showing preview data (fetching full data...)");
}
else if (query.IsSuccess)
{
    Console.WriteLine("Showing real data");
}
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
// First, we have a blog posts list query with previews
var postsListQuery = new UseQuery<List<BlogPost>>(
    new QueryOptions<List<BlogPost>>(
        queryKey: new("blogPosts"),
        queryFn: async ctx => await FetchBlogPostsListAsync()
        // Returns: { Id, Title, Snippet } - preview data
    ),
    queryClient
);

await postsListQuery.ExecuteAsync();

// Now create individual post query using preview as placeholder
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

// Immediate:
// - Shows preview (title + snippet) from cache
// - IsPlaceholderData = true
// - Status = Success (can render UI)

await postQuery.ExecuteAsync();

// After fetch:
// - Shows full content
// - IsPlaceholderData = false
```

## Complete Example: Blog Post Detail

```csharp
public class BlogPostDetail : IDisposable
{
    private readonly QueryClient _queryClient;
    
    public async Task LoadPostAsync(int postId)
    {
        var postQuery = new UseQuery<BlogPost>(
            new QueryOptions<BlogPost>(
                queryKey: new("blogPost", postId),
                queryFn: async ctx => {
                    // Fetch full blog post from server
                    Console.WriteLine($"Fetching full post {postId}...");
                    return await FetchFullBlogPostAsync(postId);
                },
                placeholderDataFunc: (_, __) => {
                    // Try to get preview from list cache
                    Console.WriteLine("Looking for preview in cache...");
                    
                    var posts = _queryClient.GetQueryData<List<BlogPost>>(new("blogPosts"));
                    var preview = posts?.Find(p => p.Id == postId);
                    
                    if (preview != null)
                    {
                        Console.WriteLine("Found preview! Showing immediately.");
                        return preview;
                    }
                    
                    Console.WriteLine("No preview found.");
                    return null;
                }
            ),
            _queryClient
        );

        // Render UI immediately if placeholder available
        RenderPost(postQuery);

        // Fetch full data in background
        await postQuery.ExecuteAsync();

        // Re-render with full data
        RenderPost(postQuery);
    }

    private void RenderPost(UseQuery<BlogPost> query)
    {
        if (query.IsPlaceholderData)
        {
            Console.WriteLine("📄 [PREVIEW]");
            Console.WriteLine($"Title: {query.Data!.Title}");
            Console.WriteLine($"Snippet: {query.Data.Snippet}");
            Console.WriteLine("Loading full content...");
        }
        else if (query.IsSuccess)
        {
            Console.WriteLine("📄 [FULL CONTENT]");
            Console.WriteLine($"Title: {query.Data!.Title}");
            Console.WriteLine($"Content: {query.Data.FullContent}");
        }
        else if (query.IsLoading)
        {
            Console.WriteLine("Loading...");
        }
    }

    public void Dispose() => _queryClient?.Dispose();
}
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
public class PaginatedPosts
{
    private int _currentPage = 1;

    public async Task LoadPageAsync(int page)
    {
        _currentPage = page;
        
        var query = new UseQuery<List<Post>>(
            new QueryOptions<List<Post>>(
                queryKey: new("posts", page),
                queryFn: async ctx => await FetchPostsPageAsync(page),
                placeholderDataFunc: (previousData, previousQuery) => {
                    // Keep showing old page data while fetching new page
                    // Prevents flickering/loading state
                    return previousData;
                }
            ),
            _queryClient
        );

        // Shows previous page as placeholder
        RenderPosts(query);

        await query.ExecuteAsync();

        // Shows new page
        RenderPosts(query);
    }

    private void RenderPosts(UseQuery<List<Post>> query)
    {
        if (query.IsPlaceholderData)
        {
            Console.WriteLine($"Showing page (loading page {_currentPage}...)");
        }
        
        foreach (var post in query.Data ?? new())
        {
            Console.WriteLine($"- {post.Title}");
        }
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

## Comparison with React Query

### React Query (TypeScript):
```typescript
// Basic placeholder
const query = useQuery({
  queryKey: ['todos'],
  queryFn: fetchTodos,
  placeholderData: placeholderTodos,
})

// From cache
const query = useQuery({
  queryKey: ['blogPost', id],
  queryFn: () => fetchPost(id),
  placeholderData: () =>
    queryClient.getQueryData(['blogPosts'])?.find(p => p.id === id),
})

// With previous data
const query = useQuery({
  queryKey: ['posts', page],
  queryFn: () => fetchPage(page),
  placeholderData: (previousData) => previousData,
})
```

### SwrSharp (C#):
```csharp
// Basic placeholder
var query = new UseQuery<List<Todo>>(
    new QueryOptions<List<Todo>>(
        queryKey: new("todos"),
        queryFn: async ctx => await FetchTodosAsync(),
        placeholderData: placeholderTodos
    ),
    queryClient
);

// From cache
var query = new UseQuery<BlogPost>(
    new QueryOptions<BlogPost>(
        queryKey: new("blogPost", id),
        queryFn: async ctx => await FetchPostAsync(id),
        placeholderDataFunc: (_, __) => {
            var posts = queryClient.GetQueryData<List<BlogPost>>(new("blogPosts"));
            return posts?.Find(p => p.Id == id);
        }
    ),
    queryClient
);

// With previous data
var query = new UseQuery<List<Post>>(
    new QueryOptions<List<Post>>(
        queryKey: new("posts", page),
        queryFn: async ctx => await FetchPageAsync(page),
        placeholderDataFunc: (previousData, previousQuery) => previousData
    ),
    queryClient
);
```

---

## Summary

- ✅ `placeholderData`: NOT persisted to cache (temporary display)
- ✅ `IsPlaceholderData`: flag to distinguish from real data
- ✅ Query starts in `Success` state (has data to display)
- ✅ Use for partial/preview data while fetching complete data
- ✅ `placeholderDataFunc`: access to previousData for transitions
- ✅ Perfect for: blog preview → full content, pagination, smooth transitions
- ✅ `initialData` takes priority over `placeholderData`
- ✅ Placeholder is replaced when real data arrives

**When to use**:
- ✅ Preview/partial data from list → detail
- ✅ Smooth page transitions
- ✅ Reduce perceived loading time
- ❌ Don't use for complete/real data (use `initialData`)

