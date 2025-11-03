
namespace BlazorQuery.Core.BuildingBlocks;

/// <summary>
/// Represents a single query that fetches data asynchronously and tracks its loading/error state.
/// </summary>
/// <typeparam name="T">The type of data returned by the query function.</typeparam>
public class UseQuery<T>
{
    private readonly QueryClient _client;
    private readonly QueryOptions<T> _queryOptions;

    public T? Data { get; private set; }
    public bool IsLoading { get; private set; }
    public bool IsSuccess => Error == null;
    public bool IsError => Error != null;
    public Exception? Error { get; private set; }

    public event Action? OnChange;

    public UseQuery(
            QueryOptions<T> queryOptions,
            QueryClient client)
    {
        _queryOptions = queryOptions;
        _client = client;
    }

    /// <summary>
    /// Executes the query, fetching data if cache is stale or missing.
    /// </summary>
    public async Task ExecuteAsync(CancellationToken? signal = null)
    {
        IsLoading = true;
        Notify();

        try
        {
            var token = signal ?? CancellationToken.None;
            var ctx = new QueryFunctionContext(_queryOptions.QueryKey, token);
            Data = await _client.FetchAsync(_queryOptions.QueryKey, (token) => _queryOptions.QueryFn(ctx), _queryOptions.StaleTime, token);
            Error = null;
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

    /// <summary>
    /// Forces refetching the query, invalidating the cache first.
    /// </summary>
    public async Task RefetchAsync(CancellationToken? signal = null)
    {
        _client.Invalidate(_queryOptions.QueryKey);
        await ExecuteAsync(signal);
    }

    private void Notify() => OnChange?.Invoke();
}
