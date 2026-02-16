using Microsoft.AspNetCore.Components;
using SwrSharp.Core;

namespace SwrSharp.Blazor;

/// <summary>
/// Base component for Blazor components that use SwrSharp hooks.
/// Provides UseQuery, UseMutation, and UseInfiniteQuery methods with automatic
/// StateHasChanged management and disposal.
/// Override <see cref="OnParametersSet"/> to register hooks. Hooks are re-evaluated
/// on every render cycle (including state changes), similar to how React re-runs
/// the component function.
/// </summary>
public abstract class SwrSharpComponentBase : ComponentBase, IAsyncDisposable
{
    [CascadingParameter] public QueryClient QueryClient { get; set; } = null!;

    private readonly List<IDisposable> _disposables = new();
    private readonly Dictionary<string, object> _hooks = new();
    private readonly List<Func<Task>> _pendingExecutions = new();
    private bool _disposed;

    protected override bool ShouldRender()
    {
        // Blazor does NOT call OnParametersSet on state changes (only on parameter changes
        // from parent). We call it here so hooks are re-evaluated with current state on
        // every render cycle — matching React's behavior of re-running hooks each render.
        OnParametersSet();
        return true;
    }

    /// <summary>
    /// Creates or retrieves a UseQuery hook. Call this in OnParametersSet.
    /// The query will auto-execute when created with a new key.
    /// </summary>
    protected UseQuery<T> UseQuery<T>(QueryOptions<T> options)
    {
        var hookKey = $"query:{options.QueryKey}";
        if (_hooks.TryGetValue(hookKey, out var existing))
            return (UseQuery<T>)existing;

        var query = new UseQuery<T>(options, QueryClient);
        query.OnChange += () => InvokeAsync(StateHasChanged);
        _hooks[hookKey] = query;
        _disposables.Add(query);
        _pendingExecutions.Add(() => query.ExecuteAsync());
        return query;
    }

    /// <summary>
    /// Creates or retrieves a UseMutation hook. Call this in OnParametersSet.
    /// Mutations are NOT auto-executed; call Mutate() or MutateAsync() explicitly.
    /// </summary>
    protected UseMutation<TData, TVariables> UseMutation<TData, TVariables>(
        MutationOptions<TData, TVariables> options)
    {
        var hookKey = $"mutation:{options.MutationKey?.ToString() ?? ("_default_mutation_" + _hooks.Count)}";
        if (_hooks.TryGetValue(hookKey, out var existing))
            return (UseMutation<TData, TVariables>)existing;

        var mutation = new UseMutation<TData, TVariables>(options, QueryClient);
        mutation.OnChange += () => InvokeAsync(StateHasChanged);
        _hooks[hookKey] = mutation;
        _disposables.Add(mutation);
        return mutation;
    }

    /// <summary>
    /// Creates or retrieves a UseInfiniteQuery hook. Call this in OnParametersSet.
    /// The query will auto-execute on first render.
    /// </summary>
    protected UseInfiniteQuery<TData, TPageParam> UseInfiniteQuery<TData, TPageParam>(
        InfiniteQueryOptions<TData, TPageParam> options)
    {
        var hookKey = $"infinite:{options.QueryKey}";
        if (_hooks.TryGetValue(hookKey, out var existing))
            return (UseInfiniteQuery<TData, TPageParam>)existing;

        var query = new UseInfiniteQuery<TData, TPageParam>(options, QueryClient);
        query.OnChange += () => InvokeAsync(StateHasChanged);
        _hooks[hookKey] = query;
        _disposables.Add(query);
        _pendingExecutions.Add(() => query.ExecuteAsync());
        return query;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_pendingExecutions.Count > 0 && !_disposed)
        {
            var tasks = _pendingExecutions.Select(execute => execute()).ToArray();
            _pendingExecutions.Clear();
            await Task.WhenAll(tasks);
        }
    }

    public virtual ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;

        foreach (var d in _disposables)
            d.Dispose();
        _disposables.Clear();
        _hooks.Clear();
        _pendingExecutions.Clear();
        return ValueTask.CompletedTask;
    }
}
