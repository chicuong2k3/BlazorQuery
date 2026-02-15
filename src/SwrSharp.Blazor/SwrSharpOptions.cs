using SwrSharp.Core;

namespace SwrSharp.Blazor;

/// <summary>
/// Configuration options for SwrSharp Blazor integration.
/// </summary>
public class SwrSharpOptions
{
    /// <summary>
    /// Default stale time for all queries. Defaults to TimeSpan.Zero.
    /// </summary>
    public TimeSpan DefaultStaleTime { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Default network mode for all queries. Defaults to NetworkMode.Online.
    /// </summary>
    public NetworkMode DefaultNetworkMode { get; set; } = NetworkMode.Online;

    /// <summary>
    /// Default refetch on window focus for all queries. Defaults to true.
    /// </summary>
    public bool DefaultRefetchOnWindowFocus { get; set; } = true;
}
