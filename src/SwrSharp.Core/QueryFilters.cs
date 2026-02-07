namespace SwrSharp.Core;

/// <summary>
/// Type of query to match in filters.
/// </summary>
public enum QueryType
{
    /// <summary>
    /// Match all queries (active and inactive).
    /// </summary>
    All,
    
    /// <summary>
    /// Match only active queries (have active observers).
    /// </summary>
    Active,
    
    /// <summary>
    /// Match only inactive queries (no active observers).
    /// </summary>
    Inactive
}

/// <summary>
/// Filters for matching queries in the cache.
/// Supports multiple matching strategies: prefix, exact, type, staleness, fetch status, and custom predicates.
/// </summary>
public class QueryFilters
{
    /// <summary>
    /// Query key to match. Matches by prefix if not exact.
    /// </summary>
    public QueryKey? QueryKey { get; set; }
    
    /// <summary>
    /// If true, only match queries with exact query key (no prefix matching).
    /// Defaults to false.
    /// </summary>
    public bool Exact { get; set; }
    
    /// <summary>
    /// Filter by query type (active/inactive/all).
    /// Defaults to All.
    /// </summary>
    public QueryType Type { get; set; } = QueryType.All;
    
    /// <summary>
    /// Filter by staleness.
    /// - true: match only stale queries
    /// - false: match only fresh queries
    /// - null: match all (default)
    /// </summary>
    public bool? Stale { get; set; }
    
    /// <summary>
    /// Filter by fetch status.
    /// - Fetching: match queries currently fetching
    /// - Paused: match queries that are paused
    /// - Idle: match queries not fetching
    /// - null: match all (default)
    /// </summary>
    public FetchStatus? FetchStatus { get; set; }
    
    /// <summary>
    /// Custom predicate function to match queries.
    /// Receives Query metadata and returns true to match.
    /// This is evaluated as a final filter after all other filters.
    /// </summary>
    public Func<QueryKey, bool>? Predicate { get; set; }

    /// <summary>
    /// Checks if a query key matches these filters.
    /// Note: This is a simplified version that only checks QueryKey and Predicate.
    /// Full matching (including Type, Stale, FetchStatus) requires query state information
    /// and should be done by QueryClient.
    /// </summary>
    public bool Matches(QueryKey queryKey)
    {
        // If predicate is provided, use it
        if (Predicate != null)
        {
            return Predicate(queryKey);
        }

        // If no QueryKey filter specified, match all
        if (QueryKey == null)
        {
            return true;
        }

        // Exact match
        if (Exact)
        {
            return QueryKey.Equals(queryKey);
        }

        // Prefix match
        return queryKey.StartsWith(QueryKey);
    }
}

