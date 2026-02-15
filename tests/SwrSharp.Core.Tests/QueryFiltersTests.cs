namespace SwrSharp.Core.Tests;

public class QueryFiltersTests
{
    [Fact]
    public void QueryFilters_DefaultValues_ShouldBeCorrect()
    {
        var filters = new QueryFilters();

        Assert.Null(filters.QueryKey);
        Assert.False(filters.Exact);
        Assert.Equal(QueryType.All, filters.Type);
        Assert.Null(filters.Stale);
        Assert.Null(filters.FetchStatus);
        Assert.Null(filters.Predicate);
    }

    [Fact]
    public void QueryFilters_Matches_NullQueryKey_ShouldMatchAll()
    {
        var filters = new QueryFilters();

        Assert.True(filters.Matches(new QueryKey("todos")));
        Assert.True(filters.Matches(new QueryKey("users")));
        Assert.True(filters.Matches(new QueryKey("posts", 1)));
    }

    [Fact]
    public void QueryFilters_Matches_PrefixMatch_ShouldWork()
    {
        var filters = new QueryFilters
        {
            QueryKey = new QueryKey("todos")
        };

        Assert.True(filters.Matches(new QueryKey("todos")));
        Assert.True(filters.Matches(new QueryKey("todos", 1)));
        Assert.True(filters.Matches(new QueryKey("todos", new { page = 1 })));
        Assert.False(filters.Matches(new QueryKey("users")));
    }

    [Fact]
    public void QueryFilters_Matches_ExactMatch_ShouldWork()
    {
        var filters = new QueryFilters
        {
            QueryKey = new QueryKey("todos"),
            Exact = true
        };

        Assert.True(filters.Matches(new QueryKey("todos")));
        Assert.False(filters.Matches(new QueryKey("todos", 1)));
        Assert.False(filters.Matches(new QueryKey("todos", new { page = 1 })));
        Assert.False(filters.Matches(new QueryKey("users")));
    }

    [Fact]
    public void QueryFilters_Matches_WithPredicate_ShouldANDWithQueryKey()
    {
        var filters = new QueryFilters
        {
            QueryKey = new QueryKey("todos"),
            Predicate = key => key.Parts.Count > 1
        };

        // QueryKey prefix must match AND predicate must pass
        Assert.False(filters.Matches(new QueryKey("todos"))); // Prefix matches but Count=1 fails predicate
        Assert.True(filters.Matches(new QueryKey("todos", 1))); // Prefix matches AND Count=2 passes
        Assert.False(filters.Matches(new QueryKey("users", 2))); // Prefix fails (not "todos")
    }

    [Fact]
    public void QueryFilters_Matches_PredicateOnly_ShouldWork()
    {
        var filters = new QueryFilters
        {
            Predicate = key => key.Parts.Count > 1
        };

        // No QueryKey filter, just predicate
        Assert.False(filters.Matches(new QueryKey("todos"))); // Count = 1
        Assert.True(filters.Matches(new QueryKey("todos", 1))); // Count = 2
        Assert.True(filters.Matches(new QueryKey("users", 2))); // Count = 2
    }

    [Fact]
    public void QueryFilters_Matches_ComplexPredicate_ShouldWork()
    {
        var filters = new QueryFilters
        {
            Predicate = key => {
                if (key.Parts.Count < 2) return false;
                if (key.Parts[0]?.ToString() != "todos") return false;

                var id = key.Parts[1] as int?;
                return id.HasValue && id.Value > 10;
            }
        };

        Assert.False(filters.Matches(new QueryKey("todos"))); // No id
        Assert.False(filters.Matches(new QueryKey("todos", 5))); // id <= 10
        Assert.True(filters.Matches(new QueryKey("todos", 15))); // id > 10
        Assert.False(filters.Matches(new QueryKey("users", 15))); // Wrong prefix
    }

    [Fact]
    public void QueryType_Enum_ShouldHaveCorrectValues()
    {
        Assert.Equal(0, (int)QueryType.All);
        Assert.Equal(1, (int)QueryType.Active);
        Assert.Equal(2, (int)QueryType.Inactive);
    }

    [Fact]
    public void QueryFilters_TypeProperty_ShouldWork()
    {
        var filters = new QueryFilters
        {
            Type = QueryType.Active
        };

        Assert.Equal(QueryType.Active, filters.Type);

        filters.Type = QueryType.Inactive;
        Assert.Equal(QueryType.Inactive, filters.Type);

        filters.Type = QueryType.All;
        Assert.Equal(QueryType.All, filters.Type);
    }

    [Fact]
    public void QueryFilters_StaleProperty_ShouldWork()
    {
        var filters = new QueryFilters
        {
            Stale = true
        };

        Assert.True(filters.Stale);

        filters.Stale = false;
        Assert.False(filters.Stale);

        filters.Stale = null;
        Assert.Null(filters.Stale);
    }

    [Fact]
    public void QueryFilters_FetchStatusProperty_ShouldWork()
    {
        var filters = new QueryFilters
        {
            FetchStatus = FetchStatus.Fetching
        };

        Assert.Equal(FetchStatus.Fetching, filters.FetchStatus);

        filters.FetchStatus = FetchStatus.Paused;
        Assert.Equal(FetchStatus.Paused, filters.FetchStatus);

        filters.FetchStatus = FetchStatus.Idle;
        Assert.Equal(FetchStatus.Idle, filters.FetchStatus);

        filters.FetchStatus = null;
        Assert.Null(filters.FetchStatus);
    }

    [Fact]
    public void QueryFilters_CombinedProperties_ShouldAllBeSet()
    {
        var filters = new QueryFilters
        {
            QueryKey = new QueryKey("todos"),
            Exact = true,
            Type = QueryType.Active,
            Stale = true,
            FetchStatus = FetchStatus.Fetching,
            Predicate = key => true
        };

        Assert.NotNull(filters.QueryKey);
        Assert.True(filters.Exact);
        Assert.Equal(QueryType.Active, filters.Type);
        Assert.True(filters.Stale);
        Assert.Equal(FetchStatus.Fetching, filters.FetchStatus);
        Assert.NotNull(filters.Predicate);
    }

    [Theory]
    [InlineData("todos", "todos", true)]
    [InlineData("todos", "users", false)]
    [InlineData("posts", "posts", true)]
    public void QueryFilters_Matches_Theory_PrefixMatch(string filterKey, string testKey, bool expected)
    {
        var filters = new QueryFilters
        {
            QueryKey = new QueryKey(filterKey)
        };

        var result = filters.Matches(new QueryKey(testKey));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("todos", "todos", true)]
    [InlineData("todos", "users", false)]
    public void QueryFilters_Matches_Theory_ExactMatch(string filterKey, string testKey, bool expected)
    {
        var filters = new QueryFilters
        {
            QueryKey = new QueryKey(filterKey),
            Exact = true
        };

        var result = filters.Matches(new QueryKey(testKey));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void QueryFilters_Matches_WithAnonymousObject_ShouldWork()
    {
        var filters = new QueryFilters
        {
            QueryKey = new QueryKey("todos", new { type = "done" })
        };

        Assert.True(filters.Matches(new QueryKey("todos", new { type = "done" })));
        Assert.True(filters.Matches(new QueryKey("todos", new { type = "done" }, 123)));
        Assert.False(filters.Matches(new QueryKey("todos", new { type = "pending" })));
    }

    [Fact]
    public void QueryFilters_Documentation_Example1()
    {
        var filters = new QueryFilters
        {
            QueryKey = new QueryKey("posts")
        };

        Assert.True(filters.Matches(new QueryKey("posts")));
        Assert.True(filters.Matches(new QueryKey("posts", 1)));
        Assert.True(filters.Matches(new QueryKey("posts", "list")));
    }

    [Fact]
    public void QueryFilters_Documentation_Example2()
    {
        var filters = new QueryFilters
        {
            QueryKey = new QueryKey("posts"),
            Exact = true
        };

        Assert.True(filters.Matches(new QueryKey("posts")));
        Assert.False(filters.Matches(new QueryKey("posts", 1)));
    }

    [Fact]
    public void QueryFilters_Documentation_Example3()
    {
        var filters = new QueryFilters
        {
            Predicate = key => {
                if (key.Parts.Count < 2) return false;
                var id = key.Parts[1] as int?;
                return id > 100;
            }
        };

        Assert.False(filters.Matches(new QueryKey("item")));
        Assert.False(filters.Matches(new QueryKey("item", 50)));
        Assert.True(filters.Matches(new QueryKey("item", 150)));
    }

    // --- MatchesWithContext tests ---

    [Fact]
    public void MatchesWithContext_TypeActive_MatchesOnlyActiveQueries()
    {
        var filters = new QueryFilters { Type = QueryType.Active };
        var key = new QueryKey("todos");

        Assert.True(filters.MatchesWithContext(key, null, isActive: true, null, null));
        Assert.False(filters.MatchesWithContext(key, null, isActive: false, null, null));
    }

    [Fact]
    public void MatchesWithContext_TypeInactive_MatchesOnlyInactiveQueries()
    {
        var filters = new QueryFilters { Type = QueryType.Inactive };
        var key = new QueryKey("todos");

        Assert.False(filters.MatchesWithContext(key, null, isActive: true, null, null));
        Assert.True(filters.MatchesWithContext(key, null, isActive: false, null, null));
    }

    [Fact]
    public void MatchesWithContext_TypeAll_MatchesBoth()
    {
        var filters = new QueryFilters { Type = QueryType.All };
        var key = new QueryKey("todos");

        Assert.True(filters.MatchesWithContext(key, null, isActive: true, null, null));
        Assert.True(filters.MatchesWithContext(key, null, isActive: false, null, null));
    }

    [Fact]
    public void MatchesWithContext_StaleTrue_MatchesOnlyStaleQueries()
    {
        var filters = new QueryFilters { Stale = true };
        var key = new QueryKey("todos");

        Assert.True(filters.MatchesWithContext(key, null, isActive: true, null, isStale: true));
        Assert.False(filters.MatchesWithContext(key, null, isActive: true, null, isStale: false));
    }

    [Fact]
    public void MatchesWithContext_StaleFalse_MatchesOnlyFreshQueries()
    {
        var filters = new QueryFilters { Stale = false };
        var key = new QueryKey("todos");

        Assert.False(filters.MatchesWithContext(key, null, isActive: true, null, isStale: true));
        Assert.True(filters.MatchesWithContext(key, null, isActive: true, null, isStale: false));
    }

    [Fact]
    public void MatchesWithContext_StaleNull_MatchesAll()
    {
        var filters = new QueryFilters { Stale = null };
        var key = new QueryKey("todos");

        Assert.True(filters.MatchesWithContext(key, null, isActive: true, null, isStale: true));
        Assert.True(filters.MatchesWithContext(key, null, isActive: true, null, isStale: false));
        Assert.True(filters.MatchesWithContext(key, null, isActive: true, null, isStale: null));
    }

    [Fact]
    public void MatchesWithContext_FetchStatus_MatchesSpecificStatus()
    {
        var key = new QueryKey("todos");

        var fetchingFilter = new QueryFilters { FetchStatus = FetchStatus.Fetching };
        Assert.True(fetchingFilter.MatchesWithContext(key, null, true, FetchStatus.Fetching, null));
        Assert.False(fetchingFilter.MatchesWithContext(key, null, true, FetchStatus.Idle, null));
        Assert.False(fetchingFilter.MatchesWithContext(key, null, true, null, null));

        var idleFilter = new QueryFilters { FetchStatus = FetchStatus.Idle };
        Assert.True(idleFilter.MatchesWithContext(key, null, true, FetchStatus.Idle, null));
        Assert.False(idleFilter.MatchesWithContext(key, null, true, FetchStatus.Fetching, null));

        var pausedFilter = new QueryFilters { FetchStatus = FetchStatus.Paused };
        Assert.True(pausedFilter.MatchesWithContext(key, null, true, FetchStatus.Paused, null));
        Assert.False(pausedFilter.MatchesWithContext(key, null, true, FetchStatus.Idle, null));
    }

    [Fact]
    public void MatchesWithContext_FetchStatusNull_MatchesAll()
    {
        var filters = new QueryFilters { FetchStatus = null };
        var key = new QueryKey("todos");

        Assert.True(filters.MatchesWithContext(key, null, true, FetchStatus.Fetching, null));
        Assert.True(filters.MatchesWithContext(key, null, true, FetchStatus.Idle, null));
        Assert.True(filters.MatchesWithContext(key, null, true, null, null));
    }

    [Fact]
    public void MatchesWithContext_CombinedFilters_AllMustMatch()
    {
        var filters = new QueryFilters
        {
            QueryKey = new QueryKey("todos"),
            Type = QueryType.Active,
            Stale = true,
            FetchStatus = FetchStatus.Idle
        };

        var key = new QueryKey("todos", 1);

        // All conditions met
        Assert.True(filters.MatchesWithContext(key, null, isActive: true, FetchStatus.Idle, isStale: true));

        // Key doesn't match
        Assert.False(filters.MatchesWithContext(new QueryKey("users"), null, isActive: true, FetchStatus.Idle, isStale: true));

        // Not active
        Assert.False(filters.MatchesWithContext(key, null, isActive: false, FetchStatus.Idle, isStale: true));

        // Not stale
        Assert.False(filters.MatchesWithContext(key, null, isActive: true, FetchStatus.Idle, isStale: false));

        // Wrong fetch status
        Assert.False(filters.MatchesWithContext(key, null, isActive: true, FetchStatus.Fetching, isStale: true));
    }

    [Fact]
    public void MatchesWithContext_PredicateANDedWithOtherFilters()
    {
        var filters = new QueryFilters
        {
            QueryKey = new QueryKey("todos"),
            Type = QueryType.Active,
            Predicate = key => key.Parts.Count > 1
        };

        // Key matches, active, predicate passes
        Assert.True(filters.MatchesWithContext(new QueryKey("todos", 1), null, isActive: true, null, null));

        // Key matches, active, predicate fails (count=1)
        Assert.False(filters.MatchesWithContext(new QueryKey("todos"), null, isActive: true, null, null));

        // Key matches, not active
        Assert.False(filters.MatchesWithContext(new QueryKey("todos", 1), null, isActive: false, null, null));

        // Key doesn't match
        Assert.False(filters.MatchesWithContext(new QueryKey("users", 1), null, isActive: true, null, null));
    }

    // --- QueryClient integration tests ---

    [Fact]
    public void QueryClient_RemoveQueries_RemovesMatchingEntries()
    {
        using var client = new QueryClient();
        client.Set(new QueryKey("todos", 1), "item1");
        client.Set(new QueryKey("todos", 2), "item2");
        client.Set(new QueryKey("users", 1), "user1");

        client.RemoveQueries(new QueryFilters { QueryKey = new QueryKey("todos") });

        Assert.Null(client.Get<string>(new QueryKey("todos", 1)));
        Assert.Null(client.Get<string>(new QueryKey("todos", 2)));
        Assert.Equal("user1", client.Get<string>(new QueryKey("users", 1)));
    }

    [Fact]
    public void QueryClient_RemoveQueries_NoFilter_RemovesAll()
    {
        using var client = new QueryClient();
        client.Set(new QueryKey("todos"), "item");
        client.Set(new QueryKey("users"), "user");

        client.RemoveQueries();

        Assert.Null(client.Get<string>(new QueryKey("todos")));
        Assert.Null(client.Get<string>(new QueryKey("users")));
    }

    [Fact]
    public void QueryClient_ResetQueries_ClearsCache()
    {
        using var client = new QueryClient();
        client.Set(new QueryKey("todos"), "item");

        client.ResetQueries(new QueryFilters { QueryKey = new QueryKey("todos") });

        Assert.Null(client.Get<string>(new QueryKey("todos")));
    }

    [Fact]
    public void QueryClient_GetFetchingCount_ReturnsZero_WhenNoActiveQueries()
    {
        using var client = new QueryClient();
        client.Set(new QueryKey("todos"), "item");

        Assert.Equal(0, client.GetFetchingCount());
    }

    [Fact]
    public void QueryClient_InvalidateQueries_WithTypeFilter_Active()
    {
        using var client = new QueryClient();
        client.Set(new QueryKey("todos"), "item");
        client.Set(new QueryKey("users"), "user");

        // Register "todos" as active
        var info = new QueryClient.ActiveQueryInfo
        {
            GetFetchStatus = () => FetchStatus.Idle,
            StaleTime = TimeSpan.FromMinutes(5)
        };
        client.RegisterActiveQuery(new QueryKey("todos"), info);

        var invalidated = new List<QueryKey>();
        client.OnQueriesInvalidated += keys => invalidated.AddRange(keys);

        // Invalidate only active queries
        client.InvalidateQueries(new QueryFilters { Type = QueryType.Active });

        Assert.Contains(new QueryKey("todos"), invalidated);
        Assert.DoesNotContain(new QueryKey("users"), invalidated);

        client.UnregisterActiveQuery(new QueryKey("todos"), info);
    }

    [Fact]
    public void QueryClient_InvalidateQueries_WithTypeFilter_Inactive()
    {
        using var client = new QueryClient();
        client.Set(new QueryKey("todos"), "item");
        client.Set(new QueryKey("users"), "user");

        // Register "todos" as active
        var info = new QueryClient.ActiveQueryInfo();
        client.RegisterActiveQuery(new QueryKey("todos"), info);

        var invalidated = new List<QueryKey>();
        client.OnQueriesInvalidated += keys => invalidated.AddRange(keys);

        // Invalidate only inactive queries
        client.InvalidateQueries(new QueryFilters { Type = QueryType.Inactive });

        Assert.DoesNotContain(new QueryKey("todos"), invalidated);
        Assert.Contains(new QueryKey("users"), invalidated);

        client.UnregisterActiveQuery(new QueryKey("todos"), info);
    }

    [Fact]
    public void QueryClient_RefetchQueries_FiresEvent()
    {
        using var client = new QueryClient();
        client.Set(new QueryKey("todos"), "item");

        var refetched = new List<QueryKey>();
        client.OnQueriesRefetched += keys => refetched.AddRange(keys);

        client.RefetchQueries(new QueryFilters { QueryKey = new QueryKey("todos") });

        Assert.Contains(new QueryKey("todos"), refetched);
    }

    [Fact]
    public void QueryClient_GetFetchingCount_WithActiveQuery()
    {
        using var client = new QueryClient();
        client.Set(new QueryKey("todos"), "item");
        client.Set(new QueryKey("users"), "user");

        var fetchingInfo = new QueryClient.ActiveQueryInfo
        {
            GetFetchStatus = () => FetchStatus.Fetching,
            StaleTime = TimeSpan.Zero
        };
        var idleInfo = new QueryClient.ActiveQueryInfo
        {
            GetFetchStatus = () => FetchStatus.Idle,
            StaleTime = TimeSpan.Zero
        };

        client.RegisterActiveQuery(new QueryKey("todos"), fetchingInfo);
        client.RegisterActiveQuery(new QueryKey("users"), idleInfo);

        Assert.Equal(1, client.GetFetchingCount());
        Assert.Equal(1, client.GetFetchingCount(new QueryFilters { QueryKey = new QueryKey("todos") }));
        Assert.Equal(0, client.GetFetchingCount(new QueryFilters { QueryKey = new QueryKey("users") }));

        client.UnregisterActiveQuery(new QueryKey("todos"), fetchingInfo);
        client.UnregisterActiveQuery(new QueryKey("users"), idleInfo);
    }
}
