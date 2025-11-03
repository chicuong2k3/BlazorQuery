using BlazorQuery.Core.BuildingBlocks;

namespace BlazorQuery.Core.Tests;

public class UseQueryTests
{
    private readonly QueryClient _client = new QueryClient();

    [Fact]
    public async Task FetchData_ReturnsExpectedResult()
    {
        var query = new UseQuery<List<string>>(
            key: new QueryKey("todos"),
            fetchFn: async ctx => await FakeApi.GetTodosAsync(),
            client: _client
        );

        await query.ExecuteAsync();

        Assert.False(query.IsError);
        Assert.NotNull(query.Data);
        Assert.Equal(2, query.Data.Count);
    }

    [Fact]
    public async Task FetchData_ErrorIsHandled()
    {
        var query = new UseQuery<string>(
            key: new QueryKey("todo", -1),
            fetchFn: async ctx => await FakeApi.GetTodoByIdAsync((int)ctx.QueryKey[1]!),
            client: _client
        );

        await query.ExecuteAsync();

        Assert.True(query.IsError);
        Assert.NotNull(query.Error);
        Assert.Equal("Invalid ID", query.Error.Message);
    }

    [Fact]
    public async Task CacheWorks()
    {
        int callCount = 0;

        var query = new UseQuery<List<string>>(
            key: new QueryKey("todos"),
            fetchFn: async ctx => {
                callCount++;
                return await FakeApi.GetTodosAsync();
            },
            client: _client,
            staleTime: TimeSpan.FromSeconds(10)
        );

        await query.ExecuteAsync();
        await query.ExecuteAsync();

        Assert.Equal(1, callCount); 
    }

    [Fact]
    public async Task RefetchInvalidatesCache()
    {
        int callCount = 0;

        var query = new UseQuery<List<string>>(
            key: new QueryKey("todos"),
            fetchFn: async ctx => {
                callCount++;
                return await FakeApi.GetTodosAsync();
            },
            client: _client,
            staleTime: TimeSpan.FromSeconds(10)
        );

        await query.ExecuteAsync();
        await query.RefetchAsync();

        Assert.Equal(2, callCount);
    }
}
