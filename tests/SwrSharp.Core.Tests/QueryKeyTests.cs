namespace SwrSharp.Core.Tests;

public class QueryKeyTests
{
    [Fact]
    public void Equal_WhenSameSimpleParts_ShouldBeTrue()
    {
        var key1 = new QueryKey("todos");
        var key2 = new QueryKey("todos");

        Assert.Equal(key1, key2);
        Assert.Equal(key1.GetHashCode(), key2.GetHashCode());
    }

    [Fact]
    public void NotEqual_WhenDifferentSimpleParts_ShouldBeFalse()
    {
        var key1 = new QueryKey("todos");
        var key2 = new QueryKey("users");

        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void Equal_WhenSameCompositeParts_ShouldBeTrue()
    {
        var key1 = new QueryKey("todo", 5, new { preview = true });
        var key2 = new QueryKey("todo", 5, new { preview = true });

        Assert.Equal(key1, key2);
        Assert.Equal(key1.GetHashCode(), key2.GetHashCode());
    }

    [Fact]
    public void NotEqual_WhenDifferentOrderOfParts_ShouldBeFalse()
    {
        var key1 = new QueryKey("todo", 5, "active");
        var key2 = new QueryKey("todo", "active", 5);

        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void Equal_WhenAnonymousPropertyOrderDiffers_ShouldBeTrue()
    {
        var key1 = new QueryKey("todos", new { page = 2, status = "active" });
        var key2 = new QueryKey("todos", new { status = "active", page = 2 });

        Assert.Equal(key1, key2);
        Assert.Equal(key1.GetHashCode(), key2.GetHashCode());
    }

    [Fact]
    public void Equal_WhenRecordTypesHaveSameValues_ShouldBeTrue()
    {
        var rec1 = new TodoRecord(1, "done");
        var rec2 = new TodoRecord(1, "done");

        var key1 = new QueryKey("todo", rec1);
        var key2 = new QueryKey("todo", rec2);

        Assert.Equal(key1, key2);
        Assert.Equal(key1.GetHashCode(), key2.GetHashCode());
    }

    [Fact]
    public void NotEqual_WhenRecordTypesDiffer_ShouldBeFalse()
    {
        var rec1 = new TodoRecord(1, "done");
        var rec2 = new TodoRecord(2, "done");

        var key1 = new QueryKey("todo", rec1);
        var key2 = new QueryKey("todo", rec2);

        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void Equal_WhenCollectionsAreSame_ShouldBeTrue()
    {
        var key1 = new QueryKey("nested", new[] { 1, 2, 3 });
        var key2 = new QueryKey("nested", new[] { 1, 2, 3 });

        Assert.Equal(key1, key2);
        Assert.Equal(key1.GetHashCode(), key2.GetHashCode());
    }

    [Fact]
    public void NotEqual_WhenCollectionsDiffer_ShouldBeFalse()
    {
        var key1 = new QueryKey("nested", new[] { 1, 2, 3 });
        var key2 = new QueryKey("nested", new[] { 3, 2, 1 });

        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void Equal_WhenNullParts_ShouldBeTrue()
    {
        var key1 = new QueryKey("todo", null);
        var key2 = new QueryKey("todo", null);

        Assert.Equal(key1, key2);
    }

    [Fact]
    public void NotEqual_WhenOnePartIsNull_ShouldBeFalse()
    {
        var key1 = new QueryKey("todo", null);
        var key2 = new QueryKey("todo", 1);

        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void ToString_ShouldIncludeAllParts()
    {
        var key = new QueryKey("todo", 5, new { preview = true });
        var str = key.ToString();

        Assert.Contains("todo", str);
        Assert.Contains("5", str);
        Assert.Contains("preview", str);
    }

    [Fact]
    public void DeterministicHash_ShouldBeStableAcrossInstances()
    {
        var key1 = new QueryKey("todos", new { page = 2, status = "active" });
        var key2 = new QueryKey("todos", new { status = "active", page = 2 });

        var hash1 = key1.GetHashCode();
        var hash2 = key2.GetHashCode();

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void Equal_WhenAnonymousHasExtraNullProperty_ShouldBeTrue()
    {
        // Null properties in anonymous objects should be ignored
        var key1 = new QueryKey("todos", new { page = 2, status = "active" });
        var key2 = new QueryKey("todos", new { page = 2, status = "active", other = (string?)null });

        Assert.Equal(key1, key2);
        Assert.Equal(key1.GetHashCode(), key2.GetHashCode());
    }

    [Fact]
    public void Equal_WhenBothAnonymousHaveDifferentNullProperties_ShouldBeTrue()
    {
        // Both have different null properties - should still be equal (nulls ignored)
        var key1 = new QueryKey("todos", new { page = 2, extra1 = (int?)null });
        var key2 = new QueryKey("todos", new { page = 2, extra2 = (string?)null });

        Assert.Equal(key1, key2);
        Assert.Equal(key1.GetHashCode(), key2.GetHashCode());
    }

    [Fact]
    public void NotEqual_WhenAnonymousHasExtraNonNullProperty_ShouldBeFalse()
    {
        // Extra non-null property should make them different
        var key1 = new QueryKey("todos", new { page = 2 });
        var key2 = new QueryKey("todos", new { page = 2, extra = "value" });

        Assert.NotEqual(key1, key2);
    }

    private record TodoRecord(int Id, string Status);
}
