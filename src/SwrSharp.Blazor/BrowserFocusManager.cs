using Microsoft.JSInterop;
using SwrSharp.Core;

namespace SwrSharp.Blazor;

/// <summary>
/// Browser-aware focus manager that uses JS interop to detect tab visibility changes.
/// Enables refetchOnWindowFocus to work correctly in Blazor WebAssembly.
/// </summary>
public sealed class BrowserFocusManager(IJSRuntime jsRuntime) : IFocusManager, IAsyncDisposable
{
    private IJSObjectReference? _module;
    private IJSObjectReference? _listener;
    private DotNetObjectReference<BrowserFocusManager>? _dotNetRef;
    private bool _initialized;

    public bool IsFocused { get; private set; } = true;

    public event Action<bool>? FocusChanged;

    /// <summary>
    /// Initializes the JS interop listener. Must be called after the first render.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;

        _module = await jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/SwrSharp.Blazor/swrsharp-interop.js");
        IsFocused = await _module.InvokeAsync<bool>("isFocused");
        _dotNetRef = DotNetObjectReference.Create(this);
        _listener = await _module.InvokeAsync<IJSObjectReference>("addFocusListener", _dotNetRef);
    }

    [JSInvokable]
    public void OnFocusChanged(bool isFocused)
    {
        if (IsFocused == isFocused) return;
        IsFocused = isFocused;
        FocusChanged?.Invoke(isFocused);
    }

    public void SetFocused(bool? isFocused)
    {
        if (isFocused == null) return;
        var previous = IsFocused;
        IsFocused = isFocused.Value;
        if (previous != IsFocused)
            FocusChanged?.Invoke(IsFocused);
    }

    public void SetEventListener(Func<Action<bool>, Action>? setupHandler)
    {
        // Not used in browser context — JS interop handles events directly
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
