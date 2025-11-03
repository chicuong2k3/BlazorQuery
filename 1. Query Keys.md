

At its core, BlazorQuery manages query caching based on *query keys*. 

In BlazorQuery, query keys are represented by the `QueryKey` class, 
which stores one or more parts that together define a unique identity for a query.

As long as your key's values are serializable and uniquely represent the data being fetched, 
BlazorQuery can deterministically identify and cache that query's result.

# The QueryKey Class

`QueryKey` is a small, immutable utility type that:
- Stores one or more parts that uniquely describe a query.
- Provides deterministic equality based on deep comparison of all parts.
- Computes a stable hash code from those parts to ensure consistent caching behavior.

Unlike typical `object.Equals`, QueryKey performs deep equality. 
Arrays, lists, anonymous types, and record types are all compared by value, not by reference.

> Dictionary and other unordered collections are not supported in query keys, 
because their order is non-deterministic and can lead to unstable hashes.

# Simple Query Keys

A **simple query key** is usually a single constant or string literal.
It's ideal when your query always fetches the same dataset, independent of parameters.

```csharp
// A global list of todos
var key = new QueryKey("todos");

// A shared application setting
var key = new QueryKey("appSettings");

// A constant cache key for global data
var key = new QueryKey("userRoles");
```

Use these when the fetched data never changes based on input variables.

# Composite Query Keys with Variables

When a query depends on parameters (IDs, page numbers, filters), 
use a **composite query key**. 

Each part contributes to the unique identity of the cached result. 
It's similar to how a function's arguments define its output. 

```csharp
// A single todo by ID
var key = new QueryKey("todo", 5);

// A paged query
var key = new QueryKey("todos", new { page = 2, size = 10 });

// A filtered query
var key = new QueryKey("todos", new { status = "done" });

// A specific todo in preview mode
var key = new QueryKey("todo", 5, new { preview = true });
```

# Deterministic Equality

Equality in `QueryKey` is value-based, not reference-based.

The following are equal, even though the anonymous properties are declared in different orders: 

```csharp
new QueryKey("todos", new { status = "active", page = 2 });
new QueryKey("todos", new { page = 2, status = "active" });
new QueryKey("todos", new { page = 2, status = "active", other = (string?)null });
```

This is because:
- Property order in anonymous or record types doesn't matter.
- Null values are considered equal.
- Deep comparisons are performed for nested objects and enumerables.

However, the order of parts matters, since each argument's position contributes to 
the key's identity. Two keys with the same values but in different positions are considered distinct. 

```csharp
// These are NOT equal
new QueryKey("todos", "active", 2);
new QueryKey("todos", 2, "active");
new QueryKey("todos", null, 2, "active");
```

This ensures predictable, deterministic caching behavior.

# Include Variables That Affect Fetching

Always include every variable that affects the data being fetched. 
Your query key acts as a dependency signature.

Whenever any part of the key changes, BlazorQuery automatically refetches the data.
```csharp
var query = new UseQuery<Todo>(
    key: new QueryKey("todo", todoId),
    fetchFn: () => FetchTodoById(todoId),
    client
);
```

Here, including `todoId` in the key ensures:
- Each `todoId` has its own cached result.
- Changing `todoId` triggers a new fetch.
- Cached data for other todos remains intact.
