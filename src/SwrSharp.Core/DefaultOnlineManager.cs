namespace SwrSharp.Core;

internal class DefaultOnlineManager : IOnlineManager
{
    private bool _isOnline;
    public bool IsOnline
    {
        get { return _isOnline; } 
        set 
        {
            if (_isOnline != value)
            {
                _isOnline = value;
                OnlineStatusChanged?.Invoke();
            }
        }
    }
    public event Action? OnlineStatusChanged;

    public DefaultOnlineManager(bool initialOnline = true)
    {
        _isOnline = initialOnline;
    }
}
