# Design Decisions: Type-Safe Default Query Functions

## Problem with Direct Port Approach

When initially porting from React Query (JavaScript/TypeScript), the temptation is to directly translate the API:

### ❌ Direct Port (Object-Based):
```csharp
// React Query style - single function returns object
public class QueryClient
{
    public Func<QueryFunctionContext, Task<object>>? DefaultQueryFn { get; set; }
}

// Usage requires runtime casting
var queryClient = new QueryClient
{
    DefaultQueryFn = async ctx => {
        // Returns object - loses type information
        var response = await httpClient.GetAsync(endpoint);
        return await response.Content.ReadFromJsonAsync<object>();
    }
};

// UseQuery must cast at runtime
public UseQuery(QueryOptions<T> options, QueryClient client)
{
    if (options.QueryFn == null && client.DefaultQueryFn != null)
    {
        // RUNTIME CASTING - can fail!
        options.QueryFn = async ctx => {
            var result = await client.DefaultQueryFn(ctx);
            if (result is T typedResult)
                return typedResult;
            throw new InvalidCastException(...); // Runtime error!
        };
    }
}
```

**Problems:**
1. ❌ Runtime casting can fail
2. ❌ No compile-time type safety
3. ❌ Performance overhead (boxing/unboxing)
4. ❌ Poor IntelliSense experience
5. ❌ Errors discovered at runtime, not compile-time
6. ❌ Not idiomatic C#

## ✅ C#-Idiomatic Solution (Type-Safe):

```csharp
// Type-safe per-type storage
public class QueryClient
{
    private readonly ConcurrentDictionary<Type, object> _defaultQueryFns = new();

    // Register type-specific function
    public void SetDefaultQueryFn<T>(Func<QueryFunctionContext, Task<T>> queryFn)
    {
        _defaultQueryFns[typeof(T)] = queryFn;
    }

    // Retrieve type-specific function
    internal Func<QueryFunctionContext, Task<T>>? GetDefaultQueryFn<T>()
    {
        if (_defaultQueryFns.TryGetValue(typeof(T), out var fn))
        {
            return fn as Func<QueryFunctionContext, Task<T>>;
        }
        return null;
    }
}

// Usage is type-safe
var queryClient = new QueryClient();

queryClient.SetDefaultQueryFn<List<Post>>(async ctx => {
    // Returns List<Post> - type information preserved
    var response = await httpClient.GetAsync(endpoint);
    return await response.Content.ReadFromJsonAsync<List<Post>>()
           ?? new List<Post>();
});

// UseQuery uses type-safe function directly
public UseQuery(QueryOptions<T> options, QueryClient client)
{
    if (options.QueryFn == null)
    {
        // NO CASTING - type-safe retrieval
        var defaultFn = client.GetDefaultQueryFn<T>();
        if (defaultFn == null)
        {
            throw new InvalidOperationException(
                $"No default function registered for {typeof(T).Name}"
            );
        }
        options.QueryFn = defaultFn; // Direct assignment!
    }
}
```

**Benefits:**
1. ✅ Compile-time type safety
2. ✅ No runtime casting needed
3. ✅ Better performance (no boxing/unboxing)
4. ✅ Excellent IntelliSense support
5. ✅ Errors caught at compile-time
6. ✅ Idiomatic C# design
7. ✅ Per-type customization

## Comparison

### React Query (TypeScript)
```typescript
// One function for all types
const defaultQueryFn = async ({ queryKey }) => {
  const data = await fetch(queryKey[0]);
  return data.json(); // Type lost, returns 'any'
}

const queryClient = new QueryClient({
  defaultOptions: {
    queries: { queryFn: defaultQueryFn }
  }
})

// Type must be manually specified
const { data } = useQuery<Post[]>({ queryKey: ['/posts'] })
//                          ^^^^^^ Manual type annotation needed
```

### BlazorQuery (C#)
```csharp
// Per-type functions
queryClient.SetDefaultQueryFn<List<Post>>(async ctx => {
    var response = await httpClient.GetAsync(ctx.QueryKey.Parts[0]);
    return await response.Content.ReadFromJsonAsync<List<Post>>();
    //           ^^^^^^^^ Type preserved throughout
});

// Type is inferred and enforced
var query = new UseQuery<List<Post>>(
    new QueryOptions<List<Post>>(queryKey: new("/posts")),
    //                  ^^^^^^^^^^ Type enforced at compile-time
    queryClient
);
// query.Data is List<Post> - fully typed!
```

## Real-World Example

### Scenario: Multi-Type API

```csharp
public class TypedApiClient
{
    private readonly QueryClient _queryClient;
    private readonly HttpClient _httpClient;

    public TypedApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _queryClient = new QueryClient();
        
        // Register different handlers for different types
        _queryClient.SetDefaultQueryFn<List<Post>>(FetchPostsList);
        _queryClient.SetDefaultQueryFn<Post>(FetchSinglePost);
        _queryClient.SetDefaultQueryFn<List<User>>(FetchUsersList);
        _queryClient.SetDefaultQueryFn<User>(FetchSingleUser);
        _queryClient.SetDefaultQueryFn<PagedResult<Post>>(FetchPagedPosts);
    }

    // Type-specific logic for lists
    private async Task<List<Post>> FetchPostsList(QueryFunctionContext ctx)
    {
        var endpoint = ctx.QueryKey.Parts[0]?.ToString();
        var response = await _httpClient.GetAsync(endpoint, ctx.Signal);
        return await response.Content.ReadFromJsonAsync<List<Post>>(
            cancellationToken: ctx.Signal
        ) ?? new List<Post>(); // Return empty list on null
    }

    // Type-specific logic for single items
    private async Task<Post> FetchSinglePost(QueryFunctionContext ctx)
    {
        var endpoint = ctx.QueryKey.Parts[0]?.ToString();
        var response = await _httpClient.GetAsync(endpoint, ctx.Signal);
        return await response.Content.ReadFromJsonAsync<Post>(
            cancellationToken: ctx.Signal
        ) ?? throw new Exception("Post not found"); // Throw on null
    }

    // Different logic for paged results
    private async Task<PagedResult<Post>> FetchPagedPosts(QueryFunctionContext ctx)
    {
        var endpoint = ctx.QueryKey.Parts[0]?.ToString();
        var page = (int)(ctx.QueryKey.Parts[1] ?? 1);
        var pageSize = (int)(ctx.QueryKey.Parts[2] ?? 10);
        
        var url = $"{endpoint}?page={page}&pageSize={pageSize}";
        var response = await _httpClient.GetAsync(url, ctx.Signal);
        return await response.Content.ReadFromJsonAsync<PagedResult<Post>>(
            cancellationToken: ctx.Signal
        ) ?? new PagedResult<Post>();
    }
}
```

With object-based approach, you would need complex switch statements and casting:

```csharp
// ❌ Object-based approach requires complex logic
private async Task<object> DefaultQueryFn(QueryFunctionContext ctx)
{
    var endpoint = ctx.QueryKey.Parts[0]?.ToString();
    
    // How do we know what type to deserialize to?
    // We would need to parse the endpoint or use metadata
    if (endpoint.Contains("/posts") && !endpoint.Contains("/"))
    {
        return await FetchList<Post>(endpoint);
    }
    else if (endpoint.Contains("/posts/"))
    {
        return await FetchSingle<Post>(endpoint);
    }
    // ... complex branching logic
    
    // And UseQuery still needs to cast!
}
```

## Performance Considerations

### Object-Based (❌ Slower):
```csharp
// 1. Box to object
object result = await fetchFunction(); 
// 2. Unbox and cast
if (result is List<Post> posts) { }
// 3. Possible InvalidCastException
```

### Type-Safe (✅ Faster):
```csharp
// Direct generic invocation - no boxing/casting
List<Post> result = await fetchFunction<List<Post>>();
// Type known at compile-time
```

## Maintainability

### Object-Based (❌ Hard to Maintain):
```csharp
// When refactoring, no compile-time checks
// Can break queries silently

// Developer changes API response
public class PostDto // Changed from Post
{
    public int Id { get; set; }
}

// Old code still compiles but fails at runtime!
var query = new UseQuery<Post>(...); // Runtime error!
```

### Type-Safe (✅ Easy to Maintain):
```csharp
// When refactoring, compile errors guide you

// Developer changes API response  
queryClient.SetDefaultQueryFn<PostDto>(async ctx => {
    return await httpClient.GetFromJsonAsync<PostDto>(...);
});

// Old code won't compile - must be updated
var query = new UseQuery<Post>(...); 
//                        ^^^^ Compile error: No default function for Post
//                             Must change to PostDto or register new function
```

## Conclusion

BlazorQuery's type-safe approach:

1. **Embraces C# Strengths**: Leverages strong typing
2. **Better Than Direct Port**: More than translation, it's adaptation
3. **Prevents Runtime Errors**: Catches issues at compile-time
4. **Improves Performance**: No runtime casting overhead
5. **Enhances Developer Experience**: Better IntelliSense, refactoring
6. **Follows .NET Idioms**: Uses patterns familiar to C# developers

**The type-safe design is not just "different" - it's objectively better for a statically-typed language like C#.**

This is a perfect example of **adapting** rather than **translating** when porting between languages with different type systems.

