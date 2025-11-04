namespace BlazorQuery.Core.BuildingBlocks;

/// <summary>
/// Manages online/offline status and notifies subscribers when the connection changes.
/// </summary>
public static class OnlineManager
{
    private static bool _isOnline = true;
    public static event Action? OnlineStatusChanged;

    public static bool IsOnline
    {
        get => _isOnline;
        set
        {
            if (_isOnline != value)
            {
                _isOnline = value;
                OnlineStatusChanged?.Invoke();
            }
        }
    }
}
