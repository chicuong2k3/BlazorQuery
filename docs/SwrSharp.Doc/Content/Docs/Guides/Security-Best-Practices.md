---
title: "Security Best Practices"
description: "Security guidelines"
order: 22
category: "Guides"
---

# Security Best Practices
Security is critical when working with data fetching and caching. Follow these best practices to keep your SwrSharp applications secure.
## Authentication & Authorization
### Secure Query Functions
```csharp
private async Task<List<Todo>> FetchUserTodos(QueryFunctionContext ctx)
{
    // Only fetch data for the current user
    var userId = await GetCurrentUserIdAsync();
    if (userId == null)
        throw new UnauthorizedAccessException("User not authenticated");
    var todos = await Http.GetFromJsonAsync<List<Todo>>(
        $"/api/users/{userId}/todos",
        ctx.Signal
    );
    return todos ?? new List<Todo>();
}
```
### Include Authentication Headers
```csharp
// Configure HttpClient with authentication
builder.Services.AddScoped(sp =>
{
    var client = new HttpClient { BaseAddress = new Uri("https://api.example.com") };
    client.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    return client;
});
```
## Cache Security
### Don't Cache Sensitive Data Too Long
```csharp
var query = new UseQuery<UserProfile>(
    new QueryOptions<UserProfile>(
        queryKey: new("user-profile"),
        queryFn: async ctx => await FetchUserProfileAsync(ctx.Signal),
        // Don't cache sensitive data for long
        staleTime: TimeSpan.FromSeconds(30)
    ),
    queryClient
);
```
### Clear Cache on Logout
```csharp
private void Logout()
{
    // Dispose the QueryClient to clear all cached data
    queryClient.Dispose();
    // Navigate to login
    NavigationManager.NavigateTo("/login");
}
```
## Input Validation
### Validate Query Parameters
```csharp
var query = new UseQuery<Product>(
    new QueryOptions<Product>(
        queryKey: new("product", productId),
        queryFn: async ctx => {
            var (queryKey, signal) = ctx;
            var id = (int?)queryKey[1];
            // Validate input
            if (id == null || id <= 0)
                throw new ArgumentException("Invalid product ID");
            return await Http.GetFromJsonAsync<Product>(
                $"/api/products/{id}",
                signal
            ) ?? throw new InvalidOperationException("Product not found");
        }
    ),
    queryClient
);
```
## Rate Limiting
### Implement Request Throttling
```csharp
public class RateLimitedFetcher
{
    private readonly SemaphoreSlim _semaphore;
    private readonly TimeSpan _timeWindow;

    public RateLimitedFetcher(int maxRequests = 10, TimeSpan? timeWindow = null)
    {
        _semaphore = new SemaphoreSlim(maxRequests, maxRequests);
        _timeWindow = timeWindow ?? TimeSpan.FromSeconds(1);
    }

    public async Task<T> FetchWithRateLimitAsync<T>(Func<Task<T>> fetchFn)
    {
        await _semaphore.WaitAsync();
        try
        {
            return await fetchFn();
        }
        finally
        {
            _ = Task.Delay(_timeWindow).ContinueWith(_ => _semaphore.Release());
        }
    }
}

// Usage in query function:
var rateLimiter = new RateLimitedFetcher(maxRequests: 10);

var query = new UseQuery<List<Product>>(
    new QueryOptions<List<Product>>(
        queryKey: new("products"),
        queryFn: async ctx => await rateLimiter.FetchWithRateLimitAsync(
            () => Http.GetFromJsonAsync<List<Product>>("/api/products", ctx.Signal)
                  ?? Task.FromResult(new List<Product>())
        )
    ),
    queryClient
);
```
## Error Information Disclosure
### Don't Expose Sensitive Errors
```csharp
@if (_query?.IsError ?? false)
{
    <div class="error-alert">
        <!-- Show generic message to users -->
        <p>An error occurred while loading data. Please try again later.</p>
        <!-- Only show detailed errors in development -->
        @if (_isDevelopment)
        {
            <details>
                <summary>Error Details</summary>
                <pre>@_query.Error?.Message</pre>
            </details>
        }
    </div>
}
@code {
    @inject IWebHostEnvironment Environment
    private bool _isDevelopment => Environment.IsDevelopment();
}
```
## HTTPS Only
### Enforce HTTPS
```csharp
// In Program.cs
var builder = WebApplication.CreateBuilder(args);
// ...
var app = builder.Build();
// Redirect HTTP to HTTPS
app.UseHttpsRedirection();
// Add security headers
app.Use(async (context, next) =>
{
    context.Response.Headers.Add("Strict-Transport-Security", "max-age=31536000");
    await next();
});
```
## Content Security Policy
### Configure CSP Headers
```csharp
// In Program.cs
app.Use(async (context, next) =>
{
    context.Response.Headers.Add("Content-Security-Policy",
        "default-src 'self'; script-src 'self' 'unsafe-inline'");
    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Add("X-Frame-Options", "DENY");
    await next();
});
```
## Sensitive Data in Query Keys
### Avoid Sensitive Data in Keys
```csharp
// BAD - password in query key
var query = new UseQuery<User>(
    new QueryOptions<User>(
        queryKey: new("user-login", email, password), // Don't do this!
        queryFn: async ctx => await LoginAsync(email, password)
    ),
    queryClient
);

// GOOD - use token/session instead
var query = new UseQuery<UserProfile>(
    new QueryOptions<UserProfile>(
        queryKey: new("user-profile"),
        queryFn: async ctx => await FetchProfileAsync(ctx.Signal) // Uses auth token from HttpClient
    ),
    queryClient
);
```
## Best Practices Checklist
- [ ] Always use HTTPS in production
- [ ] Validate all query parameters
- [ ] Don't cache sensitive data indefinitely
- [ ] Clear cache on logout
- [ ] Don't expose detailed errors to users
- [ ] Use authentication headers with HttpClient
- [ ] Implement rate limiting
- [ ] Don't include passwords/secrets in query keys
- [ ] Sanitize user input before using in queries
- [ ] Monitor for suspicious query patterns
- [ ] Keep dependencies updated for security patches
- [ ] Use Content Security Policy headers
- [ ] Implement CORS properly
