namespace BlazorQuery.Core.Tests;

public class InfiniteQueryTests : IDisposable
{
    private readonly QueryClient _client = new();

    [Fact]
    public async Task InfiniteQuery_ShouldFetchFirstPage()
    {
        var query = new UseInfiniteQuery<ProjectsPage, int?>(
            new InfiniteQueryOptions<ProjectsPage, int?>(
                queryKey: new QueryKey("projects"),
                queryFn: async (ctx, pageParam) => {
                    await Task.Delay(10);
                    return FetchProjectsPage(pageParam);
                },
                initialPageParam: 0,
                getNextPageParam: (lastPage, allPages, lastPageParam) => lastPage.NextCursor
            ),
            _client
        );

        await query.ExecuteAsync();

        Assert.True(query.IsSuccess);
        Assert.Single(query.Data.Pages);
        Assert.Equal(3, query.Data.Pages[0].Projects.Count);
        Assert.Equal(0, query.Data.PageParams[0]);
    }

    [Fact]
    public async Task InfiniteQuery_ShouldFetchNextPage()
    {
        var query = new UseInfiniteQuery<ProjectsPage, int?>(
            new InfiniteQueryOptions<ProjectsPage, int?>(
                queryKey: new QueryKey("projects"),
                queryFn: async (ctx, pageParam) => FetchProjectsPage(pageParam),
                initialPageParam: 0,
                getNextPageParam: (lastPage, allPages, lastPageParam) => lastPage.NextCursor
            ),
            _client
        );

        await query.ExecuteAsync();
        Assert.Single(query.Data.Pages);

        await query.FetchNextPageAsync();

        Assert.Equal(2, query.Data.Pages.Count);
        Assert.Equal(3, query.Data.Pages[1].Projects.Count);
        Assert.Equal(3, query.Data.PageParams[1]);
    }

    [Fact]
    public async Task InfiniteQuery_HasNextPage_ShouldBeTrue()
    {
        var query = new UseInfiniteQuery<ProjectsPage, int?>(
            new InfiniteQueryOptions<ProjectsPage, int?>(
                queryKey: new QueryKey("projects"),
                queryFn: async (ctx, pageParam) => FetchProjectsPage(pageParam),
                initialPageParam: 0,
                getNextPageParam: (lastPage, allPages, lastPageParam) => lastPage.NextCursor
            ),
            _client
        );

        await query.ExecuteAsync();

        Assert.True(query.HasNextPage);
    }

    [Fact]
    public async Task InfiniteQuery_HasNextPage_ShouldBeFalse_WhenNoMore()
    {
        var query = new UseInfiniteQuery<ProjectsPage, int?>(
            new InfiniteQueryOptions<ProjectsPage, int?>(
                queryKey: new QueryKey("projects"),
                queryFn: async (ctx, pageParam) => FetchProjectsPage(pageParam),
                initialPageParam: 0,
                getNextPageParam: (lastPage, allPages, lastPageParam) => lastPage.NextCursor
            ),
            _client
        );

        // Fetch all pages
        await query.ExecuteAsync();
        await query.FetchNextPageAsync(); // page 1
        await query.FetchNextPageAsync(); // page 2
        await query.FetchNextPageAsync(); // page 3 (last)

        Assert.False(query.HasNextPage);
    }

    [Fact]
    public async Task InfiniteQuery_ShouldNotFetchNextPage_WhenNoMore()
    {
        var query = new UseInfiniteQuery<ProjectsPage, int?>(
            new InfiniteQueryOptions<ProjectsPage, int?>(
                queryKey: new QueryKey("projects"),
                queryFn: async (ctx, pageParam) => FetchProjectsPage(pageParam),
                initialPageParam: 0,
                getNextPageParam: (lastPage, allPages, lastPageParam) => lastPage.NextCursor
            ),
            _client
        );

        // Fetch all pages
        await query.ExecuteAsync();
        while (query.HasNextPage)
        {
            await query.FetchNextPageAsync();
        }

        var pageCount = query.Data.Pages.Count;

        // Try to fetch one more (should do nothing)
        await query.FetchNextPageAsync();

        Assert.Equal(pageCount, query.Data.Pages.Count);
    }

    [Fact]
    public async Task InfiniteQuery_IsFetchingNextPage_ShouldBeTrue()
    {
        var tcs = new TaskCompletionSource<ProjectsPage>();
        
        var query = new UseInfiniteQuery<ProjectsPage, int?>(
            new InfiniteQueryOptions<ProjectsPage, int?>(
                queryKey: new QueryKey("projects"),
                queryFn: async (ctx, pageParam) => {
                    if (pageParam == 0)
                        return FetchProjectsPage(0);
                    return await tcs.Task;
                },
                initialPageParam: 0,
                getNextPageParam: (lastPage, allPages, lastPageParam) => lastPage.NextCursor
            ),
            _client
        );

        await query.ExecuteAsync();
        
        var fetchTask = query.FetchNextPageAsync();
        await Task.Delay(20);

        Assert.True(query.IsFetchingNextPage);
        Assert.True(query.IsFetching);

        tcs.SetResult(FetchProjectsPage(3));
        await fetchTask;

        Assert.False(query.IsFetchingNextPage);
        Assert.False(query.IsFetching);
    }

    [Fact]
    public async Task InfiniteQuery_ShouldFetchPreviousPage()
    {
        var query = new UseInfiniteQuery<ProjectsPage, int?>(
            new InfiniteQueryOptions<ProjectsPage, int?>(
                queryKey: new QueryKey("projects"),
                queryFn: async (ctx, pageParam) => FetchProjectsPage(pageParam),
                initialPageParam: 3, // Start from page 1
                getNextPageParam: (lastPage, allPages, lastPageParam) => lastPage.NextCursor,
                getPreviousPageParam: (firstPage, allPages, firstPageParam) => firstPage.PrevCursor
            ),
            _client
        );

        await query.ExecuteAsync();
        Assert.Single(query.Data.Pages);

        await query.FetchPreviousPageAsync();

        Assert.Equal(2, query.Data.Pages.Count);
        Assert.Equal(0, query.Data.PageParams[0]); // Previous page param
    }

    [Fact]
    public async Task InfiniteQuery_HasPreviousPage_ShouldWork()
    {
        var query = new UseInfiniteQuery<ProjectsPage, int?>(
            new InfiniteQueryOptions<ProjectsPage, int?>(
                queryKey: new QueryKey("projects"),
                queryFn: async (ctx, pageParam) => FetchProjectsPage(pageParam),
                initialPageParam: 3, // Start from page 1
                getNextPageParam: (lastPage, allPages, lastPageParam) => lastPage.NextCursor,
                getPreviousPageParam: (firstPage, allPages, firstPageParam) => firstPage.PrevCursor
            ),
            _client
        );

        await query.ExecuteAsync();

        Assert.True(query.HasPreviousPage); // Can go to page 0

        await query.FetchPreviousPageAsync();

        Assert.False(query.HasPreviousPage); // At page 0, no previous
    }

    [Fact]
    public async Task InfiniteQuery_MaxPages_ShouldLimitPageCount()
    {
        var query = new UseInfiniteQuery<ProjectsPage, int?>(
            new InfiniteQueryOptions<ProjectsPage, int?>(
                queryKey: new QueryKey("projects"),
                queryFn: async (ctx, pageParam) => FetchProjectsPage(pageParam),
                initialPageParam: 0,
                getNextPageParam: (lastPage, allPages, lastPageParam) => lastPage.NextCursor,
                maxPages: 2 // Keep only 2 pages
            ),
            _client
        );

        await query.ExecuteAsync();
        await query.FetchNextPageAsync();
        await query.FetchNextPageAsync();

        // Should have only 2 pages (oldest removed)
        Assert.Equal(2, query.Data.Pages.Count);
        Assert.Equal(3, query.Data.PageParams[0]); // Page 1
        Assert.Equal(6, query.Data.PageParams[1]); // Page 2
    }

    [Fact]
    public async Task InfiniteQuery_Refetch_ShouldRefetchAllPages()
    {
        var fetchCount = 0;
        
        var query = new UseInfiniteQuery<ProjectsPage, int?>(
            new InfiniteQueryOptions<ProjectsPage, int?>(
                queryKey: new QueryKey("projects"),
                queryFn: async (ctx, pageParam) => {
                    fetchCount++;
                    return FetchProjectsPage(pageParam);
                },
                initialPageParam: 0,
                getNextPageParam: (lastPage, allPages, lastPageParam) => lastPage.NextCursor
            ),
            _client
        );

        await query.ExecuteAsync(); // 1 fetch
        await query.FetchNextPageAsync(); // 2 fetches
        
        fetchCount = 0; // Reset

        await query.RefetchAsync();

        // Should refetch both pages
        Assert.Equal(2, fetchCount);
    }

    [Fact]
    public async Task InfiniteQuery_WithoutGetNextPageParam_HasNextPage_ShouldBeFalse()
    {
        var query = new UseInfiniteQuery<ProjectsPage, int?>(
            new InfiniteQueryOptions<ProjectsPage, int?>(
                queryKey: new QueryKey("projects"),
                queryFn: async (ctx, pageParam) => FetchProjectsPage(pageParam),
                initialPageParam: 0
                // No getNextPageParam
            ),
            _client
        );

        await query.ExecuteAsync();

        Assert.False(query.HasNextPage);
    }

    [Fact]
    public async Task InfiniteQuery_CancelRefetch_False_ShouldAllowSimultaneousFetch()
    {
        var delayTcs = new TaskCompletionSource<ProjectsPage>();
        var fetchCount = 0;
        
        var query = new UseInfiniteQuery<ProjectsPage, int?>(
            new InfiniteQueryOptions<ProjectsPage, int?>(
                queryKey: new QueryKey("projects"),
                queryFn: async (ctx, pageParam) => {
                    fetchCount++;
                    if (fetchCount == 2)
                        return await delayTcs.Task;
                    return FetchProjectsPage(pageParam);
                },
                initialPageParam: 0,
                getNextPageParam: (lastPage, allPages, lastPageParam) => lastPage.NextCursor
            ),
            _client
        );

        await query.ExecuteAsync();

        // Start first fetch (will block)
        var fetch1 = query.FetchNextPageAsync(cancelRefetch: false);
        await Task.Delay(20);

        // Start second fetch with cancelRefetch: false (should proceed)
        var fetch2 = query.FetchNextPageAsync(cancelRefetch: false);

        // Complete delayed fetch
        delayTcs.SetResult(FetchProjectsPage(3));
        
        await Task.WhenAll(fetch1, fetch2);

        // Both should have been attempted
        Assert.True(fetchCount >= 2);
    }

    [Fact]
    public async Task InfiniteQuery_DataStructure_ShouldMatchExpectedFormat()
    {
        var query = new UseInfiniteQuery<ProjectsPage, int?>(
            new InfiniteQueryOptions<ProjectsPage, int?>(
                queryKey: new QueryKey("projects"),
                queryFn: async (ctx, pageParam) => FetchProjectsPage(pageParam),
                initialPageParam: 0,
                getNextPageParam: (lastPage, allPages, lastPageParam) => lastPage.NextCursor
            ),
            _client
        );

        await query.ExecuteAsync();

        // Verify data structure
        Assert.NotNull(query.Data);
        Assert.NotNull(query.Data.Pages);
        Assert.NotNull(query.Data.PageParams);
        Assert.Equal(query.Data.Pages.Count, query.Data.PageParams.Count);
    }

    public void Dispose() => _client?.Dispose();

    // Helper methods
    private static ProjectsPage FetchProjectsPage(int? cursor)
    {
        var pages = new Dictionary<int, ProjectsPage>
        {
            [0] = new() {
                Projects = new() { 
                    new() { Id = 1, Name = "Project 1" },
                    new() { Id = 2, Name = "Project 2" },
                    new() { Id = 3, Name = "Project 3" }
                },
                NextCursor = 3,
                PrevCursor = null
            },
            [3] = new() {
                Projects = new() { 
                    new() { Id = 4, Name = "Project 4" },
                    new() { Id = 5, Name = "Project 5" },
                    new() { Id = 6, Name = "Project 6" }
                },
                NextCursor = 6,
                PrevCursor = 0
            },
            [6] = new() {
                Projects = new() { 
                    new() { Id = 7, Name = "Project 7" },
                    new() { Id = 8, Name = "Project 8" },
                    new() { Id = 9, Name = "Project 9" }
                },
                NextCursor = 9,
                PrevCursor = 3
            },
            [9] = new() {
                Projects = new() { 
                    new() { Id = 10, Name = "Project 10" }
                },
                NextCursor = null, // No more pages
                PrevCursor = 6
            }
        };

        return pages[cursor ?? 0];
    }

    // Helper classes
    private class ProjectsPage
    {
        public List<Project> Projects { get; set; } = new();
        public int? NextCursor { get; set; }
        public int? PrevCursor { get; set; }
    }

    private class Project
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}

