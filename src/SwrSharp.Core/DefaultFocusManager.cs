namespace SwrSharp.Core;

/// <summary>
/// Default focus manager that assumes the application is always focused.
/// Platform-specific implementations should override this behavior.
/// </summary>
public class DefaultFocusManager : IFocusManager
{
    private bool? _manualFocusState;
    private Func<Action<bool>, Action>? _customEventListener;
    private Action? _cleanupAction;

    public bool IsFocused
    {
        get
        {
            // If manually set, use that
            if (_manualFocusState.HasValue)
                return _manualFocusState.Value;

            // Default: assume always focused
            // Platform-specific implementations will override this
            return true;
        }
    }

    public event Action<bool>? FocusChanged;

    /// <summary>
    /// Manually set the focus state.
    /// Pass null to fallback to automatic detection.
    /// </summary>
    public void SetFocused(bool? isFocused)
    {
        var previousState = IsFocused;
        _manualFocusState = isFocused;
        var newState = IsFocused;

        if (previousState != newState)
        {
            FocusChanged?.Invoke(newState);
        }
    }

    /// <summary>
    /// Set up custom event listener for focus detection.
    /// </summary>
    /// <param name="setupHandler">
    /// Callback that receives a handleFocus function.
    /// Should return a dispose/cleanup action.
    /// </param>
    public void SetEventListener(Func<Action<bool>, Action>? setupHandler)
    {
        // Clean up previous listener
        _cleanupAction?.Invoke();
        _cleanupAction = null;
        _customEventListener = null;

        if (setupHandler == null)
            return;

        _customEventListener = setupHandler;

        // Set up the new listener
        // Pass a callback that the platform can call when focus changes
        void HandleFocus(bool isFocused)
        {
            var previousState = IsFocused;
            _manualFocusState = isFocused;
            var newState = IsFocused;

            if (previousState != newState)
            {
                FocusChanged?.Invoke(newState);
            }
        }

        _cleanupAction = setupHandler(HandleFocus);
    }

    public void Dispose()
    {
        _cleanupAction?.Invoke();
        _cleanupAction = null;
        FocusChanged = null;
    }
}

