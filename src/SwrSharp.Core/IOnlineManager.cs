namespace SwrSharp.Core;


public interface IOnlineManager 
{
    bool IsOnline { get; set; }
    event Action? OnlineStatusChanged;
}
