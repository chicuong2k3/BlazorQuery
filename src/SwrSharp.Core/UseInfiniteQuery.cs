namespace SwrSharp.Core;

/// <summary>
/// Infinite query for "load more" and infinite scroll patterns.
/// </summary>
public class UseInfiniteQuery<TData, TPageParam> : IDisposable
{
    private readonly InfiniteQueryOptions<TData, TPageParam> _options;
    private readonly QueryClient _client;
    private readonly SemaphoreSlim _fetchLock = new(1, 1);
    
    private InfiniteData<TData> _data = new();
    private Exception? _error;
    private bool _isFetchingNextPage;
    private bool _isFetchingPreviousPage;

    public UseInfiniteQuery(
        InfiniteQueryOptions<TData, TPageParam> options,
        QueryClient client)
    {
        _options = options;
        _client = client;
        
        // Initialize with initial page param
        _data.PageParams.Add(_options.InitialPageParam);
    }

    /// <summary>
    /// Infinite query data containing pages and page params.
    /// </summary>
    public InfiniteData<TData> Data => _data;

    public Exception? Error
    {
        get => _error;
        private set
        {
            if (_error?.Equals(value) ?? value == null)
                return;
            _error = value;
            OnChange?.Invoke();
        }
    }

    public QueryStatus Status
    {
        get
        {
            if (Error != null)
                return QueryStatus.Error;
            if (_data.Pages.Count == 0)
                return QueryStatus.Pending;
            return QueryStatus.Success;
        }
    }

    public bool IsPending => Status == QueryStatus.Pending;
    public bool IsSuccess => Status == QueryStatus.Success;
    public bool IsError => Status == QueryStatus.Error;
    public bool IsFetching { get; private set; }
    
    /// <summary>
    /// True when fetching the next page.
    /// </summary>
    public bool IsFetchingNextPage => _isFetchingNextPage;
    
    /// <summary>
    /// True when fetching the previous page.
    /// </summary>
    public bool IsFetchingPreviousPage => _isFetchingPreviousPage;
    
    /// <summary>
    /// True if there is a next page available.
    /// Based on getNextPageParam returning non-null.
    /// </summary>
    public bool HasNextPage
    {
        get
        {
            if (_options.GetNextPageParam == null || _data.Pages.Count == 0)
                return false;

            var lastPage = _data.Pages[^1];
            var lastPageParam = (TPageParam)_data.PageParams[^1]!;
            var nextParam = _options.GetNextPageParam(lastPage, _data.Pages, lastPageParam);
            
            return nextParam != null;
        }
    }
    
    /// <summary>
    /// True if there is a previous page available.
    /// Based on getPreviousPageParam returning non-null.
    /// </summary>
    public bool HasPreviousPage
    {
        get
        {
            if (_options.GetPreviousPageParam == null || _data.Pages.Count == 0)
                return false;

            var firstPage = _data.Pages[0];
            var firstPageParam = (TPageParam)_data.PageParams[0]!;
            var prevParam = _options.GetPreviousPageParam(firstPage, _data.Pages, firstPageParam);
            
            return prevParam != null;
        }
    }

    public event Action? OnChange;

    /// <summary>
    /// Fetches the first page.
    /// </summary>
    public async Task ExecuteAsync(CancellationToken? cancellationToken = null)
    {
        if (!_options.Enabled)
            return;

        await _fetchLock.WaitAsync();
        
        try
        {
            IsFetching = true;
            OnChange?.Invoke();

            var ctx = new QueryFunctionContext(_options.QueryKey, cancellationToken ?? CancellationToken.None, _options.Meta);
            var firstPage = await _options.QueryFn(ctx, _options.InitialPageParam);

            _data.Pages.Clear();
            _data.PageParams.Clear();
            
            _data.Pages.Add(firstPage);
            _data.PageParams.Add(_options.InitialPageParam);

            Error = null;
        }
        catch (Exception ex)
        {
            Error = ex;
        }
        finally
        {
            IsFetching = false;
            OnChange?.Invoke();
            _fetchLock.Release();
        }
    }

    /// <summary>
    /// Fetches the next page using getNextPageParam.
    /// </summary>
    public async Task FetchNextPageAsync(CancellationToken? cancellationToken = null, bool cancelRefetch = true)
    {
        if (!_options.Enabled)
            return;

        if (_options.GetNextPageParam == null)
            throw new InvalidOperationException("getNextPageParam is required to fetch next page");

        // Check if already fetching (unless cancelRefetch is false)
        if (cancelRefetch && IsFetching)
            return;

        if (!HasNextPage)
            return;

        await _fetchLock.WaitAsync();
        
        try
        {
            IsFetching = true;
            _isFetchingNextPage = true;
            OnChange?.Invoke();

            var lastPage = _data.Pages[^1];
            var lastPageParam = (TPageParam)_data.PageParams[^1]!;
            var nextPageParam = _options.GetNextPageParam(lastPage, _data.Pages, lastPageParam);

            if (nextPageParam == null)
                return;

            var ctx = new QueryFunctionContext(_options.QueryKey, cancellationToken ?? CancellationToken.None, _options.Meta);
            var nextPage = await _options.QueryFn(ctx, nextPageParam);

            _data.Pages.Add(nextPage);
            _data.PageParams.Add(nextPageParam);

            // Apply maxPages limit
            if (_options.MaxPages.HasValue && _data.Pages.Count > _options.MaxPages.Value)
            {
                var overflow = _data.Pages.Count - _options.MaxPages.Value;
                _data.Pages.RemoveRange(0, overflow);
                _data.PageParams.RemoveRange(0, overflow);
            }

            Error = null;
        }
        catch (Exception ex)
        {
            Error = ex;
        }
        finally
        {
            IsFetching = false;
            _isFetchingNextPage = false;
            OnChange?.Invoke();
            _fetchLock.Release();
        }
    }

    /// <summary>
    /// Fetches the previous page using getPreviousPageParam.
    /// </summary>
    public async Task FetchPreviousPageAsync(CancellationToken? cancellationToken = null, bool cancelRefetch = true)
    {
        if (!_options.Enabled)
            return;

        if (_options.GetPreviousPageParam == null)
            throw new InvalidOperationException("getPreviousPageParam is required to fetch previous page");

        // Check if already fetching (unless cancelRefetch is false)
        if (cancelRefetch && IsFetching)
            return;

        if (!HasPreviousPage)
            return;

        await _fetchLock.WaitAsync();
        
        try
        {
            IsFetching = true;
            _isFetchingPreviousPage = true;
            OnChange?.Invoke();

            var firstPage = _data.Pages[0];
            var firstPageParam = (TPageParam)_data.PageParams[0]!;
            var prevPageParam = _options.GetPreviousPageParam(firstPage, _data.Pages, firstPageParam);

            if (prevPageParam == null)
                return;

            var ctx = new QueryFunctionContext(_options.QueryKey, cancellationToken ?? CancellationToken.None, _options.Meta);
            var prevPage = await _options.QueryFn(ctx, prevPageParam);

            _data.Pages.Insert(0, prevPage);
            _data.PageParams.Insert(0, prevPageParam);

            // Apply maxPages limit from the end
            if (_options.MaxPages.HasValue && _data.Pages.Count > _options.MaxPages.Value)
            {
                var overflow = _data.Pages.Count - _options.MaxPages.Value;
                _data.Pages.RemoveRange(_data.Pages.Count - overflow, overflow);
                _data.PageParams.RemoveRange(_data.PageParams.Count - overflow, overflow);
            }

            Error = null;
        }
        catch (Exception ex)
        {
            Error = ex;
        }
        finally
        {
            IsFetching = false;
            _isFetchingPreviousPage = false;
            OnChange?.Invoke();
            _fetchLock.Release();
        }
    }

    /// <summary>
    /// Refetches all pages sequentially.
    /// </summary>
    public async Task RefetchAsync(CancellationToken? cancellationToken = null)
    {
        if (!_options.Enabled)
            return;

        await _fetchLock.WaitAsync();
        
        try
        {
            IsFetching = true;
            OnChange?.Invoke();

            var newPages = new List<TData>();
            var newPageParams = new List<object?>();

            // Create a copy of page params to iterate over
            // This prevents potential modification during enumeration issues
            var pageParamsToRefetch = new List<object?>(_data.PageParams);

            // Refetch each page sequentially using stored page params
            foreach (var pageParam in pageParamsToRefetch)
            {
                var ctx = new QueryFunctionContext(_options.QueryKey, cancellationToken ?? CancellationToken.None, _options.Meta);
                var page = await _options.QueryFn(ctx, (TPageParam)pageParam!);
                
                newPages.Add(page);
                newPageParams.Add(pageParam);
            }

            _data.Pages = newPages;
            _data.PageParams = newPageParams;

            Error = null;
        }
        catch (Exception ex)
        {
            Error = ex;
        }
        finally
        {
            IsFetching = false;
            OnChange?.Invoke();
            _fetchLock.Release();
        }
    }

    public void Dispose()
    {
        _fetchLock.Dispose();
    }
}



