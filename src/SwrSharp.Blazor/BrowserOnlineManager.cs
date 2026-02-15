using Microsoft.JSInterop;
using SwrSharp.Core;

namespace SwrSharp.Blazor;

/// <summary>
/// Browser-aware online manager that uses JS interop to detect navigator.onLine state.
/// Enables network-aware query pausing/resuming in Blazor WebAssembly.
/// </summary>
public sealed class BrowserOnlineManager(IJSRuntime jsRuntime) : IOnlineManager, IAsyncDisposable
{
    private IJSObjectReference? _module;
    private IJSObjectReference? _listener;
    private DotNetObjectReference<BrowserOnlineManager>? _dotNetRef;
    private bool _isOnline = true;
    private bool _initialized;

    public bool IsOnline
    {
        get => _isOnline;
        set
        {
            if (_isOnline == value) return;
            _isOnline = value;
            OnlineStatusChanged?.Invoke();
        }
    }

    public event Action? OnlineStatusChanged;

    /// <summary>
    /// Initializes the JS interop listener. Must be called after the first render.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;

        _module = await jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/SwrSharp.Blazor/swrsharp-interop.js");
        _isOnline = await _module.InvokeAsync<bool>("isOnline");
        _dotNetRef = DotNetObjectReference.Create(this);
        _listener = await _module.InvokeAsync<IJSObjectReference>("addOnlineListener", _dotNetRef);
    }

    [JSInvokable]
    public void OnOnlineStatusChanged(bool isOnline)
    {
        IsOnline = isOnline;
    }

    public async ValueTask DisposeAsync()
    {
        if (_listener != null)
        {
            await _listener.InvokeVoidAsync("dispose");
            await _listener.DisposeAsync();
        }
        _dotNetRef?.Dispose();
        if (_module != null)
            await _module.DisposeAsync();
    }
}
