namespace BlazorQuery.Core.Tests;

public static class FakeApi
{
    public static Task<List<string>> GetTodosAsync()
        => Task.FromResult(new List<string> { "Task 1", "Task 2" });

    public static Task<string> GetTodoByIdAsync(int id)
    {
        if (id <= 0)
            throw new Exception("Invalid ID");

        return Task.FromResult($"Todo {id}");
    }
}