namespace SwrSharp.Core;

/// <summary>
/// Options for query cancellation operations.
/// Controls the behavior when cancelling queries.
/// </summary>
public class CancelOptions
{
    /// <summary>
    /// When set to true, suppresses propagation of OperationCanceledException 
    /// to observers (e.g., onError callbacks) and related notifications.
    /// Defaults to false.
    /// </summary>
    public bool Silent { get; set; } = false;

    /// <summary>
    /// When set to true, restores the query's state (data and status) from 
    /// immediately before the in-flight fetch, sets fetchStatus back to idle,
    /// and only throws if there was no prior data.
    /// Defaults to true.
    /// </summary>
    public bool Revert { get; set; } = true;
}

