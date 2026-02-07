namespace SwrSharp.Core;

/// <summary>
/// Manages focus state across different platforms (Blazor, WPF, Avalonia, etc.)
/// </summary>
public interface IFocusManager
{
    /// <summary>
    /// Gets whether the application is currently focused.
    /// </summary>
    bool IsFocused { get; }

    /// <summary>
    /// Event fired when focus state changes.
    /// </summary>
    event Action<bool>? FocusChanged;

    /// <summary>
    /// Manually set the focus state.
    /// Pass null/undefined to fallback to automatic focus detection.
    /// </summary>
    void SetFocused(bool? isFocused);

    /// <summary>
    /// Set up custom event listener for focus detection.
    /// Returns a dispose action to clean up the listener.
    /// </summary>
    /// <param name="setupHandler">
    /// Callback that receives a handleFocus function to call when focus changes.
    /// Should return a dispose action.
    /// </param>
    void SetEventListener(Func<Action<bool>, Action>? setupHandler);
}

