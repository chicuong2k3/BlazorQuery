
namespace BlazorQuery.Core.BuildingBlocks;

public class UseQuery<T>
{
    private readonly QueryKey _key;
    private readonly Func<Task<T>> _fetchFn;
    private readonly QueryClient _client;

    public T? Data { get; private set; }
    public bool IsLoading { get; private set; }
    public bool IsSuccess => Data != null && Error == null;
    public bool IsError => Error != null;
    public Exception? Error { get; private set; }

    public event Action? OnChange;

    public UseQuery(QueryKey key, Func<Task<T>> fetchFn, QueryClient client)
    {
        _key = key;
        _fetchFn = fetchFn;
        _client = client;
    }

    public async Task ExecuteAsync(TimeSpan? staleTime = null)
    {
        try
        {
            IsLoading = true;
            Notify();

            Data = await _client.FetchAsync(_key, _fetchFn, staleTime);
        }
        catch (Exception ex)
        {
            Error = ex;
        }
        finally
        {
            IsLoading = false;
            Notify();
        }
    }

    public async Task RefetchAsync()
    {
        _client.Invalidate(_key);
        await ExecuteAsync();
    }

    private void Notify() => OnChange?.Invoke();
}
