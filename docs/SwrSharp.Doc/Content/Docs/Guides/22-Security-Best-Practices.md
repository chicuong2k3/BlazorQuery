---
title: "Security Best Practices"
description: "Guide for Security Best Practices in SwrSharp"
order: 22
category: "Guides"
---
# Security Best Practices
Security is critical when working with data fetching and caching. Follow these best practices to keep your SwrSharp applications secure.
## Authentication & Authorization
### Secure Query Functions
```csharp
private async Task<Todo[]> FetchUserTodos(QueryFunctionContext ctx)
{
    // Only fetch data for the current user
    var userId = await GetCurrentUserIdAsync();
    if (userId == null)
        throw new UnauthorizedAccessException("User not authenticated");
    var todos = await Http.GetFromJsonAsync<Todo[]>(
        $"/api/users/{userId}/todos", 
        ctx.Signal
    );
    return todos ?? Array.Empty<Todo>();
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
### Don't Cache Sensitive Data
```csharp
var query = await QueryClient.UseQuery(
    queryKey: new QueryKey("user-profile"),
    queryFn: FetchUserProfile,
    options: new QueryOptions
    {
        // Don't cache sensitive data for long
        StaleTime = TimeSpan.FromSeconds(30),
        // Enable garbage collection quickly
        GcTime = TimeSpan.FromMinutes(1)
    }
);
```
### Clear Cache on Logout
```csharp
private async Task LogoutAsync()
{
    // Clear all cached data when user logs out
    await QueryClient.ClearCache();
    // Navigate to login
    NavigationManager.NavigateTo("/login");
}
```
## Input Validation
### Validate Query Parameters
```csharp
private async Task<Product> FetchProduct(QueryFunctionContext ctx)
{
    var (queryKey, signal) = ctx;
    var productId = (int?)queryKey[1];
    // Validate input
    if (productId == null || productId <= 0)
        throw new ArgumentException("Invalid product ID");
    return await Http.GetFromJsonAsync<Product>(
        $"/api/products/{productId}", 
        signal
    ) ?? throw new InvalidOperationException("Product not found");
}
```
## Rate Limiting
### Implement Request Throttling
```csharp
public class RateLimitedQueryClient
{
    private readonly QueryClient queryClient;
    private readonly SemaphoreSlim semaphore;
    private readonly int maxRequests;
    private readonly TimeSpan timeWindow;
    public RateLimitedQueryClient(QueryClient queryClient, int maxRequests = 10, TimeSpan? timeWindow = null)
    {
        this.queryClient = queryClient;
        this.maxRequests = maxRequests;
        this.timeWindow = timeWindow ?? TimeSpan.FromSeconds(1);
        this.semaphore = new SemaphoreSlim(maxRequests, maxRequests);
    }
    public async Task<T> UseQueryWithRateLimit<T>(QueryKey key, Func<QueryFunctionContext, Task<T>> fn)
    {
        await semaphore.WaitAsync();
        try
        {
            return await queryClient.UseQuery(key, fn);
        }
        finally
        {
            _ = Task.Delay(timeWindow).ContinueWith(_ => semaphore.Release());
        }
    }
}
```
## Error Information Disclosure
### Don't Expose Sensitive Errors
```csharp
@if (Query?.IsError ?? false)
{
    <div class="error-alert">
        <!-- Show generic message to users -->
        <p>An error occurred while loading data. Please try again later.</p>
        <!-- Only show detailed errors in development -->
        @if (isDevelopment)
        {
            <details>
                <summary>Error Details</summary>
                <pre>@Query.Error?.Message</pre>
            </details>
        }
    </div>
}
@code {
    @inject IWebHostEnvironment Environment
    private bool isDevelopment => Environment.IsDevelopment();
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
var query = await QueryClient.UseQuery(
    queryKey: new QueryKey("user-login", email, password), // ✗ Don't do this!
    queryFn: Login
);
// GOOD - use token/session instead
var query = await QueryClient.UseQuery(
    queryKey: new QueryKey("user-profile"),
    queryFn: FetchProfile // Uses authentication token from HttpClient
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
