namespace SwrSharp.Core;

public class MutationOptions<TData, TVariables>
{
    public required Func<TVariables, Task<TData>> MutationFn { get; init; }
    public QueryKey? MutationKey { get; init; }
    public int Retry { get; init; } = 0;
    public TimeSpan? RetryDelay { get; init; }
    public Func<int, TimeSpan>? RetryDelayFunc { get; init; }
    public TimeSpan? MaxRetryDelay { get; init; }
    public NetworkMode NetworkMode { get; init; } = NetworkMode.Online;
    public IReadOnlyDictionary<string, object>? Meta { get; init; }
    public MutationScope? Scope { get; init; }

    /// <summary>
    /// Called before the mutation function fires. Return value is passed as onMutateResult to other callbacks.
    /// </summary>
    public Func<TVariables, MutationContext, Task<object?>>? OnMutate { get; init; }

    /// <summary>
    /// Called when the mutation succeeds.
    /// </summary>
    public Func<TData, TVariables, object?, MutationContext, Task>? OnSuccess { get; init; }

    /// <summary>
    /// Called when the mutation fails.
    /// </summary>
    public Func<Exception, TVariables, object?, MutationContext, Task>? OnError { get; init; }

    /// <summary>
    /// Called when the mutation settles (success or error).
    /// </summary>
    public Func<TData?, Exception?, TVariables, object?, MutationContext, Task>? OnSettled { get; init; }
}
