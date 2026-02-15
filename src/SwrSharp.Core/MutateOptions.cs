namespace SwrSharp.Core;

public class MutateOptions<TData, TVariables>
{
    public Func<TData, TVariables, object?, MutationContext, Task>? OnSuccess { get; init; }
    public Func<Exception, TVariables, object?, MutationContext, Task>? OnError { get; init; }
    public Func<TData?, Exception?, TVariables, object?, MutationContext, Task>? OnSettled { get; init; }
}
