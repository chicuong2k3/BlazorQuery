---
title: "Blazor Integration"
description: "Using SwrSharp.Blazor for seamless data fetching in Blazor components"
order: 1
category: "Guides"
---

# Blazor Integration

`SwrSharp.Blazor` provides a seamless integration layer between SwrSharp.Core and Blazor components. It handles automatic UI re-rendering, component lifecycle management, and browser-specific features like window focus detection and online/offline awareness.

## Architecture

```
SwrSharp.Blazor
├── AddSwrSharp()              → DI registration
├── QueryClientProvider        → CascadingValue for QueryClient
├── SwrSharpComponentBase      → Base component with UseQuery/UseMutation/UseInfiniteQuery
├── BrowserFocusManager        → JS interop for tab visibility
└── BrowserOnlineManager       → JS interop for navigator.onLine
```

## SwrSharpComponentBase

The core of the Blazor integration. Inherit from this base class to get hook-like methods.

### UseQuery

Fetches data and auto-executes on first render:

```razor
@page "/users"
@inherits SwrSharpComponentBase

@if (users.IsLoading)
{
    <p>Loading...</p>
}
else if (users.IsError)
{
    <p>Error: @users.Error?.Message</p>
}
else
{
    @foreach (var user in users.Data!)
    {
        <p>@user.Name</p>
    }
}

@code {
    [Inject] private HttpClient Http { get; set; } = null!;
    private UseQuery<List<User>> users = null!;

    protected override void OnParametersSet()
    {
        users = UseQuery(new QueryOptions<List<User>>(
            queryKey: new QueryKey("users"),
            queryFn: async ctx => await Http.GetFromJsonAsync<List<User>>("api/users"),
            staleTime: TimeSpan.FromMinutes(5)
        ));
    }
}
```

### UseMutation

For create/update/delete operations. Mutations are NOT auto-executed — call `Mutate()` or `MutateAsync()` explicitly:

```razor
@inherits SwrSharpComponentBase

<button @onclick="HandleCreate" disabled="@createUser.IsPending">
    @(createUser.IsPending ? "Creating..." : "Create User")
</button>

@code {
    [Inject] private HttpClient Http { get; set; } = null!;
    private UseMutation<User, CreateUserDto> createUser = null!;

    protected override void OnParametersSet()
    {
        createUser = UseMutation(new MutationOptions<User, CreateUserDto>
        {
            MutationFn = async dto =>
                await Http.PostAsJsonAsync<User>("api/users", dto),
            OnSuccess = async (data, vars, onMutateResult, ctx) =>
            {
                // Invalidate the users query to refetch
                ctx.Client.InvalidateQueries(new QueryFilters
                {
                    QueryKey = new QueryKey("users")
                });
            }
        });
    }

    async Task HandleCreate()
    {
        await createUser.MutateAsync(new CreateUserDto { Name = "New User" });
    }
}
```

### UseInfiniteQuery

For paginated/infinite scroll patterns:

```razor
@inherits SwrSharpComponentBase

@foreach (var page in posts.Data.Pages)
{
    @foreach (var post in page)
    {
        <div>@post.Title</div>
    }
}

@if (posts.HasNextPage)
{
    <button @onclick="() => posts.FetchNextPageAsync()"
            disabled="@posts.IsFetchingNextPage">
        @(posts.IsFetchingNextPage ? "Loading more..." : "Load More")
    </button>
}

@code {
    private UseInfiniteQuery<List<Post>, int> posts = null!;

    protected override void OnParametersSet()
    {
        posts = UseInfiniteQuery(new InfiniteQueryOptions<List<Post>, int>(
            queryKey: new QueryKey("posts"),
            queryFn: async ctx =>
            {
                var page = (int)ctx.PageParam!;
                return await Http.GetFromJsonAsync<List<Post>>($"api/posts?page={page}");
            },
            initialPageParam: 1,
            getNextPageParam: (lastPage, allPages, lastParam) =>
                lastPage.Count > 0 ? lastParam + 1 : null
        ));
    }
}
```

## Browser-Aware Managers

SwrSharp.Blazor provides browser-specific implementations that enable features which require JavaScript interop.

### BrowserFocusManager

Detects browser tab visibility changes via the `visibilitychange` event. This enables `refetchOnWindowFocus` — queries automatically refetch stale data when the user returns to the tab.

### BrowserOnlineManager

Detects browser online/offline state via `navigator.onLine` and the `online`/`offline` events. This enables:

- **NetworkMode.Online**: Queries pause when offline, resume when back online
- **refetchOnReconnect**: Stale queries automatically refetch when connection is restored

Both managers are registered and initialized automatically by `AddSwrSharp()` and `QueryClientProvider`.

## QueryClientProvider

Provides the `QueryClient` as a `CascadingValue` to all child components. Also initializes the browser JS interop managers on first render.

```razor
<!-- Routes.razor -->
<QueryClientProvider>
    <Router AppAssembly="typeof(Program).Assembly">
        <Found Context="routeData">
            <RouteView RouteData="routeData" DefaultLayout="typeof(Layout.MainLayout)"/>
        </Found>
    </Router>
</QueryClientProvider>
```

You can also pass an explicit `QueryClient`:

```razor
<QueryClientProvider Client="@myCustomClient">
    ...
</QueryClientProvider>
```

## How Auto-Rendering Works

When you call `UseQuery()`, `UseMutation()`, or `UseInfiniteQuery()` in `OnParametersSet`:

1. The hook subscribes to the `OnChange` event of the underlying Core type
2. Every state change (data loaded, error, fetching status) triggers `InvokeAsync(StateHasChanged)`
3. This marshals the re-render back to Blazor's synchronization context (thread-safe)
4. Queries auto-execute on `OnAfterRenderAsync(firstRender: true)`
5. All hooks are automatically disposed when the component is disposed

## Accessing QueryClient Directly

The `QueryClient` is available as a cascading parameter on `SwrSharpComponentBase`:

```razor
@inherits SwrSharpComponentBase

@code {
    void InvalidateAll()
    {
        QueryClient.InvalidateQueries();
    }

    void SetCache()
    {
        QueryClient.Set(new QueryKey("key"), "value");
    }
}
```

## Best Practices

- Always register hooks in `OnParametersSet`, not `OnInitialized` — this ensures hooks are created before the first render
- Hooks are keyed by their `QueryKey`/`MutationKey`, so calling `UseQuery` with the same key returns the same instance
- Use `staleTime` to control how often data is refetched
- Prefer `MutateAsync` over `Mutate` when you need to await completion or handle errors in the UI flow
