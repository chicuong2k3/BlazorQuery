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
    /// Checks if a query key matches these filters (key-only matching).
    /// Does not check Type, Stale, or FetchStatus.
    /// Predicate is AND'd with key matching, not a replacement.
    /// </summary>
    public bool Matches(QueryKey queryKey)
    {
        // Step 1: QueryKey matching
        if (QueryKey != null)
        {
            if (Exact)
            {
                if (!QueryKey.Equals(queryKey))
                    return false;
            }
            else
            {
                if (!queryKey.StartsWith(QueryKey))
                    return false;
            }
        }

        // Step 2: Predicate as final AND filter
        if (Predicate != null)
        {
            return Predicate(queryKey);
        }

        return true;
    }

    /// <summary>
    /// Context-aware matching that checks all filter properties including Type, Stale, and FetchStatus.
    /// Used internally by QueryClient for full filter evaluation.
    /// </summary>
    internal bool MatchesWithContext(
        QueryKey queryKey,
        QueryClient.CacheEntry? entry,
        bool isActive,
        FetchStatus? currentFetchStatus,
        bool? isStale)
    {
        // Step 1: QueryKey matching
        if (QueryKey != null)
        {
            if (Exact)
            {
                if (!QueryKey.Equals(queryKey))
                    return false;
            }
            else
            {
                if (!queryKey.StartsWith(QueryKey))
                    return false;
            }
        }

        // Step 2: Type filter
        if (Type != QueryType.All)
        {
            if (Type == QueryType.Active && !isActive)
                return false;
            if (Type == QueryType.Inactive && isActive)
                return false;
        }

        // Step 3: Stale filter
        if (Stale.HasValue && isStale.HasValue)
        {
            if (Stale.Value != isStale.Value)
                return false;
        }

        // Step 4: FetchStatus filter
        if (FetchStatus.HasValue)
        {
            if (!currentFetchStatus.HasValue || currentFetchStatus.Value != FetchStatus.Value)
                return false;
        }

        // Step 5: Predicate as final AND filter
        if (Predicate != null)
        {
            return Predicate(queryKey);
        }

        return true;
    }
}
