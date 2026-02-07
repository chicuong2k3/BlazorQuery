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
    public void QueryFilters_Matches_WithPredicate_ShouldUsePredicateOnly()
    {
        var filters = new QueryFilters
        {
            QueryKey = new QueryKey("todos"), // This is ignored when Predicate is set
            Predicate = key => key.Parts.Count > 1
        };

        // Predicate overrides QueryKey matching
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

    [Fact]
    public void QueryFilters_Matches_ShouldNotConsiderTypeStaleOrFetchStatus()
    {
        // Note: Matches() method only checks QueryKey and Predicate
        // Type, Stale, and FetchStatus require query state and should be
        // checked by QueryClient, not by the Matches method itself
        
        var filters = new QueryFilters
        {
            QueryKey = new QueryKey("todos"),
            Type = QueryType.Active,
            Stale = true,
            FetchStatus = FetchStatus.Fetching
        };

        // Matches only checks QueryKey match, not Type/Stale/FetchStatus
        Assert.True(filters.Matches(new QueryKey("todos")));
        Assert.True(filters.Matches(new QueryKey("todos", 1)));
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

        // Should match with same anonymous object structure
        Assert.True(filters.Matches(new QueryKey("todos", new { type = "done" })));
        
        // Should match with additional parts (prefix matching)
        Assert.True(filters.Matches(new QueryKey("todos", new { type = "done" }, 123)));
        
        // Should not match with different anonymous object
        Assert.False(filters.Matches(new QueryKey("todos", new { type = "pending" })));
    }

    [Fact]
    public void QueryFilters_Documentation_Example1()
    {
        // Example from documentation: Match all posts queries
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
        // Example from documentation: Exact match only
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
        // Example from documentation: Custom predicate for id > 100
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
}

