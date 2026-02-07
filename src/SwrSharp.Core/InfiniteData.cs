namespace SwrSharp.Core;

/// <summary>
/// Data structure for infinite query results.
/// Contains all pages and their parameters.
/// </summary>
public class InfiniteData<TData>
{
    /// <summary>
    /// Array of fetched pages.
    /// </summary>
    public List<TData> Pages { get; set; } = new();
    
    /// <summary>
    /// Array of page params used to fetch the pages.
    /// </summary>
    public List<object?> PageParams { get; set; } = new();
}

