---
title: "Window Focus Refetching"
description: "Refetch on window focus"
order: 10
category: "Guides"
---

# Window Focus Refetching

If a user leaves your application and returns and the query data is stale, **SwrSharp automatically requests fresh data for you in the background**. You can disable this globally or per-query using the `refetchOnWindowFocus` option.

> Note: SwrSharp follows the React Query behavior and terminology where `staleTime: 0` means the data is immediately stale (i.e. "always stale"). See the React Query docs for the canonical behavior: https://tanstack.com/query/latest/docs

## Basic Usage

### Disabling Per-Query

```csharp
var query = new UseQuery<List<Todo>>(
    new QueryOptions<List<Todo>>(
        queryKey: new("todos"),
        queryFn: async ctx => await FetchTodosAsync(),
        refetchOnWindowFocus: false // Disable for this query
    ),
    queryClient
);
```

### Disabling Globally

```csharp
var queryClient = new QueryClient()
{
    DefaultRefetchOnWindowFocus = false // Disable for all queries
};
```

> Important: At the moment SwrSharp's `QueryOptions` constructor sets `refetchOnWindowFocus` to `true` by default. That means an individual query will still opt-in to focus refetching unless you explicitly set `refetchOnWindowFocus: false` on the `QueryOptions` instance. The `QueryClient.DefaultRefetchOnWindowFocus` field exists to allow libraries and higher-level code to store a global preference, but code must explicitly opt into using that global value. If you want your queries to inherit the client's global default, set the per-query option accordingly or consider wrapping `QueryOptions` construction to read `QueryClient.DefaultRefetchOnWindowFocus`.

## How It Works

When `refetchOnWindowFocus` is `true` (default):
1. User navigates away from your app (window loses focus)
2. Data becomes stale while user is away
3. User returns to your app (window gains focus)
4. SwrSharp automatically refetches stale queries in the background

```csharp
var query = new UseQuery<Data>(
    new QueryOptions<Data>(
        queryKey: new("data"),
        queryFn: async ctx => await FetchDataAsync(),
        staleTime: TimeSpan.FromMinutes(5),
        refetchOnWindowFocus: true // Default
    ),
    queryClient
);

// When user returns:
// - If data is < 5 minutes old: No refetch (still fresh)
// - If data is > 5 minutes old: Automatic background refetch
```

## Platform-Specific Implementations

SwrSharp provides an abstraction (`IFocusManager`) that allows different platforms to implement focus detection their own way.

### Default Implementation (Always Focused)

The default `DefaultFocusManager` assumes the application is always focused. This is suitable for:
- Server-side applications
- Background services
- Non-interactive applications

```csharp
// Default behavior (always focused)
var queryClient = new QueryClient();
// Uses DefaultFocusManager internally
```

### Blazor WebAssembly

For Blazor WebAssembly, implement focus detection using JavaScript Interop:

```csharp
public class BlazorFocusManager : IFocusManager
{
    private readonly IJSRuntime _jsRuntime;
    private bool _isFocused = true;
    private DotNetObjectReference<BlazorFocusManager>? _objRef;

    public bool IsFocused => _isFocused;
    public event Action<bool>? FocusChanged;

    public BlazorFocusManager(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
        InitializeAsync();
    }

    private async void InitializeAsync()
    {
        _objRef = DotNetObjectReference.Create(this);
        
        // Setup JavaScript focus listener
        await _jsRuntime.InvokeVoidAsync(
            "window.blazorQueryFocusManager.initialize",
            _objRef
        );
    }

    [JSInvokable]
    public void OnFocusChanged(bool isFocused)
    {
        if (_isFocused != isFocused)
        {
            _isFocused = isFocused;
            FocusChanged?.Invoke(isFocused);
        }
    }

    public void SetFocused(bool? isFocused)
    {
        if (isFocused.HasValue && _isFocused != isFocused.Value)
        {
            _isFocused = isFocused.Value;
            FocusChanged?.Invoke(_isFocused);
        }
    }

    public void SetEventListener(Func<Action<bool>, Action>? setupHandler)
    {
        // Custom event listener setup
        // Not typically needed for Blazor
    }

    public void Dispose()
    {
        _objRef?.Dispose();
    }
}
```

**JavaScript (wwwroot/js/focusManager.js)**:
```javascript
window.blazorQueryFocusManager = {
    dotNetRef: null,
    
    initialize: function(dotNetRef) {
        this.dotNetRef = dotNetRef;
        
        // Listen to visibility change
        document.addEventListener('visibilitychange', () => {
            const isFocused = document.visibilityState === 'visible';
            dotNetRef.invokeMethodAsync('OnFocusChanged', isFocused);
        });
        
        // Also listen to window focus/blur
        window.addEventListener('focus', () => {
            dotNetRef.invokeMethodAsync('OnFocusChanged', true);
        });
        
        window.addEventListener('blur', () => {
            dotNetRef.invokeMethodAsync('OnFocusChanged', false);
        });
    }
};
```

**Usage**:
```csharp
@inject IJSRuntime JSRuntime

@code {
    protected override async Task OnInitializedAsync()
    {
        var focusManager = new BlazorFocusManager(JSRuntime);
        var queryClient = new QueryClient(focusManager: focusManager);
        
        // Now queries will refetch when window gains focus
    }
}
```

### WPF

For WPF applications, use the `Activated`/`Deactivated` events:

```csharp
public class WpfFocusManager : IFocusManager
{
    private readonly Window _window;
    private bool _isFocused;

    public bool IsFocused => _isFocused;
    public event Action<bool>? FocusChanged;

    public WpfFocusManager(Window window)
    {
        _window = window;
        _isFocused = _window.IsActive;

        _window.Activated += OnWindowActivated;
        _window.Deactivated += OnWindowDeactivated;
    }

    private void OnWindowActivated(object? sender, EventArgs e)
    {
        if (!_isFocused)
        {
            _isFocused = true;
            FocusChanged?.Invoke(true);
        }
    }

    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
        if (_isFocused)
        {
            _isFocused = false;
            FocusChanged?.Invoke(false);
        }
    }

    public void SetFocused(bool? isFocused)
    {
        if (isFocused.HasValue && _isFocused != isFocused.Value)
        {
            _isFocused = isFocused.Value;
            FocusChanged?.Invoke(_isFocused);
        }
    }

    public void SetEventListener(Func<Action<bool>, Action>? setupHandler)
    {
        // Custom event listener if needed
    }

    public void Dispose()
    {
        _window.Activated -= OnWindowActivated;
        _window.Deactivated -= OnWindowDeactivated;
    }
}
```

**Usage**:
```csharp
public partial class MainWindow : Window
{
    private readonly QueryClient _queryClient;

    public MainWindow()
    {
        InitializeComponent();
        
        var focusManager = new WpfFocusManager(this);
        _queryClient = new QueryClient(focusManager: focusManager);
    }
}
```

### Avalonia

For Avalonia applications:

```csharp
public class AvaloniaFocusManager : IFocusManager
{
    private readonly Window _window;
    private bool _isFocused;

    public bool IsFocused => _isFocused;
    public event Action<bool>? FocusChanged;

    public AvalonilaFocusManager(Window window)
    {
        _window = window;
        _isFocused = window.IsActive;

        _window.Activated += OnWindowActivated;
        _window.Deactivated += OnWindowDeactivated;
    }

    private void OnWindowActivated(object? sender, EventArgs e)
    {
        if (!_isFocused)
        {
            _isFocused = true;
            FocusChanged?.Invoke(true);
        }
    }

    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
        if (_isFocused)
        {
            _isFocused = false;
            FocusChanged?.Invoke(false);
        }
    }

    public void SetFocused(bool? isFocused)
    {
        if (isFocused.HasValue && _isFocused != isFocused.Value)
        {
            _isFocused = isFocused.Value;
            FocusChanged?.Invoke(_isFocused);
        }
    }

    public void SetEventListener(Func<Action<bool>, Action>? setupHandler)
    {
        // Custom event listener if needed
    }

    public void Dispose()
    {
        _window.Activated -= OnWindowActivated;
        _window.Deactivated -= OnWindowDeactivated;
    }
}
```

### MAUI

For .NET MAUI applications:

```csharp
public class MauiFocusManager : IFocusManager
{
    private bool _isFocused = true;

    public bool IsFocused => _isFocused;
    public event Action<bool>? FocusChanged;

    public MauiFocusManager()
    {
        // Subscribe to application lifecycle events
        Application.Current.RequestedThemeChanged += OnAppLifecycleChanged;
    }

    private void OnAppLifecycleChanged(object? sender, AppThemeChangedEventArgs e)
    {
        // Use platform-specific APIs
#if ANDROID
        var activity = Platform.CurrentActivity;
        var isFocused = activity?.HasWindowFocus ?? false;
        UpdateFocusState(isFocused);
#elif IOS || MACCATALYST
        var isFocused = UIKit.UIApplication.SharedApplication.ApplicationState == 
                        UIKit.UIApplicationState.Active;
        UpdateFocusState(isFocused);
#elif WINDOWS
        var window = Application.Current?.Windows?.FirstOrDefault()?.Handler?.PlatformView as 
                     Microsoft.UI.Xaml.Window;
        var isFocused = window?.Visible ?? false;
        UpdateFocusState(isFocused);
#endif
    }

    private void UpdateFocusState(bool isFocused)
    {
        if (_isFocused != isFocused)
        {
            _isFocused = isFocused;
            FocusChanged?.Invoke(isFocused);
        }
    }

    public void SetFocused(bool? isFocused)
    {
        if (isFocused.HasValue)
            UpdateFocusState(isFocused.Value);
    }

    public void SetEventListener(Func<Action<bool>, Action>? setupHandler)
    {
        // Custom listener if needed
    }
}
```

## Custom Event Listener

In rare circumstances, you may want to manage your own focus events. Use `SetEventListener` on the QueryClient's FocusManager:

```csharp
var queryClient = new QueryClient();

queryClient.FocusManager.SetEventListener((handleFocus) => {
    // Your custom focus detection logic
    
    // Example: Using a timer to poll focus state
    var timer = new System.Timers.Timer(1000);
    timer.Elapsed += (s, e) => {
        // Check if window is focused (platform-specific)
        bool isFocused = CheckIfWindowIsFocused();
        handleFocus(isFocused);
    };
    timer.Start();
    
    // Return cleanup action
    return () => {
        timer.Stop();
        timer.Dispose();
    };
});
```

## Managing Focus State Manually

You can manually override the focus state:

```csharp
var queryClient = new QueryClient();

// Override to unfocused (prevent refetches)
queryClient.FocusManager.SetFocused(false);

// Override to focused (trigger refetches)
queryClient.FocusManager.SetFocused(true);

// Fallback to automatic detection
queryClient.FocusManager.SetFocused(null);
```

## Complete Example

```csharp
public class TodoApp : IDisposable
{
    private readonly QueryClient _queryClient;
    private readonly IFocusManager _focusManager;
    private UseQuery<List<Todo>>? _todosQuery;

    public TodoApp(IFocusManager focusManager)
    {
        _focusManager = focusManager;
        _queryClient = new QueryClient(focusManager: focusManager);
        
        // Monitor focus changes for logging
        _focusManager.FocusChanged += OnFocusChanged;
    }

    public async Task InitializeAsync()
    {
        _todosQuery = new UseQuery<List<Todo>>(
            new QueryOptions<List<Todo>>(
                queryKey: new("todos"),
                queryFn: async ctx => {
                    Console.WriteLine("Fetching todos...");
                    return await FetchTodosAsync();
                },
                staleTime: TimeSpan.FromMinutes(5),
                refetchOnWindowFocus: true // Will refetch when window gains focus
            ),
            _queryClient
        );

        _todosQuery.OnChange += RenderUI;
        await _todosQuery.ExecuteAsync();
    }

    private void OnFocusChanged(bool isFocused)
    {
        if (isFocused)
        {
            Console.WriteLine("✅ Window gained focus - checking for stale data...");
        }
        else
        {
            Console.WriteLine("⏸️ Window lost focus");
        }
    }

    private void RenderUI()
    {
        if (_todosQuery == null) return;

        if (_todosQuery.IsFetchingBackground)
        {
            Console.WriteLine("🔄 Refreshing todos in background...");
        }

        if (_todosQuery.IsSuccess && _todosQuery.Data != null)
        {
            Console.WriteLine($"📋 {_todosQuery.Data.Count} todos loaded");
        }
    }

    public void Dispose()
    {
        _focusManager.FocusChanged -= OnFocusChanged;
        _todosQuery?.Dispose();
        _queryClient.Dispose();
    }
}
```

## When to Disable

Consider disabling `refetchOnWindowFocus` when:

1. **Data changes infrequently**: If your data rarely changes, automatic refetching wastes resources
2. **User is actively editing**: Refetching might disrupt user input or form state
3. **Expensive queries**: Very slow or costly queries shouldn't refetch too often
4. **Real-time data**: If you're using WebSockets or SignalR for real-time updates, you don't need focus refetching

```csharp
// Disable for static reference data
var countriesQuery = new UseQuery<List<Country>>(
    new QueryOptions<List<Country>>(
        queryKey: new("countries"),
        queryFn: async ctx => await FetchCountriesAsync(),
        staleTime: TimeSpan.FromHours(24),
        refetchOnWindowFocus: false // Static data, no need to refetch
    ),
    queryClient
);

// Enable for dynamic data
var notificationsQuery = new UseQuery<List<Notification>>(
    new QueryOptions<List<Notification>>(
        queryKey: new("notifications"),
        queryFn: async ctx => await FetchNotificationsAsync(),
        staleTime: TimeSpan.FromMinutes(1),
        refetchOnWindowFocus: true // Always get latest notifications
    ),
    queryClient
);
```
