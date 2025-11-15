namespace BlazorQuery.Core.BuildingBlocks;


public interface IOnlineManager 
{
    bool IsOnline { get; set; }
    event Action? OnlineStatusChanged;
}
