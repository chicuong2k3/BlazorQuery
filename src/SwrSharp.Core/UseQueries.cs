namespace SwrSharp.Core;

public class UseQueries<T> : IDisposable
{
    private readonly QueryClient _client;
    private readonly List<UseQuery<T>> _queries = new();
    private readonly List<Action> _unsubscribeActions = new();

    public IReadOnlyList<UseQuery<T>> Queries => _queries;
    public event Action? OnChange;

    public UseQueries(QueryClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <summary>
    /// Set the list of QueryOptions; will dispose and recreate queries as needed.
    /// </summary>
    public void SetQueries(IEnumerable<QueryOptions<T>> options)
    {
        // Dispose old queries and unsubscribe
        foreach (var unsub in _unsubscribeActions) unsub();
        _unsubscribeActions.Clear();

        foreach (var q in _queries) q.Dispose();
        _queries.Clear();

        // Create new queries
        foreach (var opt in options)
        {
            var query = new UseQuery<T>(opt, _client);
            _queries.Add(query);

            // subscribe to each query's OnChange -> bubble up
            void Handler() => OnChange?.Invoke();
            query.OnChange += Handler;

            _unsubscribeActions.Add(() => query.OnChange -= Handler);
        }

        // notify initial state
        OnChange?.Invoke();
    }

    /// <summary>
    /// Optionally trigger execute for all queries in parallel.
    /// </summary>
    public Task ExecuteAllAsync(CancellationToken? ct = null)
    {
        var tasks = _queries.Select(q => q.ExecuteAsync(ct)).ToArray();
        return Task.WhenAll(tasks);
    }

    public Task RefetchAllAsync(CancellationToken? ct = null)
    {
        var tasks = _queries.Select(q => q.RefetchAsync(ct)).ToArray();
        return Task.WhenAll(tasks);
    }

    public void Dispose()
    {
        foreach (var unsub in _unsubscribeActions) unsub();
        _unsubscribeActions.Clear();

        foreach (var q in _queries) q.Dispose();
        _queries.Clear();
    }
}


public class UseQueries : IDisposable
{
    private readonly QueryClient _client;
    private readonly List<IDisposable> _queries = new();
    private readonly List<Action> _unsubscribeActions = new();

    /// <summary>
    /// Access to the underlying query instances.
    /// Use reflection or cast to access typed data.
    /// </summary>
    public IReadOnlyList<IDisposable> Queries => _queries;
    
    public event Action? OnChange;

    public UseQueries(QueryClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public void SetQueries(IEnumerable<(object queryOptions, Type type)> queryDefinitions)
    {
        // Dispose old queries & unsubscribe
        foreach (var unsub in _unsubscribeActions) unsub();
        _unsubscribeActions.Clear();

        foreach (var q in _queries) q.Dispose();
        _queries.Clear();

        foreach (var (optionsObj, type) in queryDefinitions)
        {
            // Use reflection to create UseQuery<T>
            var queryType = typeof(UseQuery<>).MakeGenericType(type);
            var query = (IDisposable)Activator.CreateInstance(queryType, optionsObj, _client)!;

            _queries.Add(query);

            // Subscribe to OnChange -> bubble up
            var onChangeEvent = queryType.GetEvent(nameof(OnChange))!;
            void Handler() => OnChange?.Invoke();
            onChangeEvent.AddEventHandler(query, Handler);

            _unsubscribeActions.Add(() => onChangeEvent.RemoveEventHandler(query, Handler));
        }

        // Notify initial state
        OnChange?.Invoke();
    }

    /// <summary>
    /// Execute all queries in parallel
    /// </summary>
    public Task ExecuteAllAsync(CancellationToken? ct = null)
    {
        var executeTasks = _queries.Select(q =>
        {
            var execMethod = q.GetType().GetMethod("ExecuteAsync")!;
            var task = (Task)execMethod.Invoke(q, [ct])!;
            return task;
        }).ToArray();

        return Task.WhenAll(executeTasks);
    }

    /// <summary>
    /// Refetch all queries in parallel
    /// </summary>
    public Task RefetchAllAsync(CancellationToken? ct = null)
    {
        var tasks = _queries.Select(q =>
        {
            var refetchMethod = q.GetType().GetMethod("RefetchAsync")!;
            return (Task)refetchMethod.Invoke(q, [ct])!;
        }).ToArray();

        return Task.WhenAll(tasks);
    }

    public void Dispose()
    {
        foreach (var unsub in _unsubscribeActions) unsub();
        _unsubscribeActions.Clear();

        foreach (var q in _queries) q.Dispose();
        _queries.Clear();
    }
}