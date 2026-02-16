---
title: "Dependent Queries"
description: "Queries based on other results"
order: 7
category: "Guides"
---

Dependent (or serial) queries depend on previous ones to finish before they can execute. This is done **reactively** — you subscribe to `OnChange` and trigger dependent queries when data becomes available, allowing your application to handle intermediate loading states.

## Basic Dependent Query

Use the `OnChange` event to reactively trigger a dependent query when the first one completes:

```csharp
var userQuery = new UseQuery<User>(
    new QueryOptions<User>(
        queryKey: new("user", email),
        queryFn: async ctx => await GetUserByEmailAsync(email)
    ),
    queryClient
);

UseQuery<List<Project>>? projectsQuery = null;

userQuery.OnChange += () =>
{
    if (userQuery.IsSuccess && userQuery.Data != null)
    {
        var userId = userQuery.Data.Id;

        // Dispose previous dependent query if exists
        projectsQuery?.Dispose();

        projectsQuery = new UseQuery<List<Project>>(
            new QueryOptions<List<Project>>(
                queryKey: new("projects", userId),
                queryFn: async ctx => await GetProjectsByUserAsync(userId)
            ),
            queryClient
        );

        projectsQuery.OnChange += () =>
        {
            // Notify your UI framework to re-render
        };

        _ = projectsQuery.ExecuteAsync();
    }

    // Notify your UI framework to re-render
};

_ = userQuery.ExecuteAsync();
```

The key pattern: create the dependent query **inside** the `OnChange` handler of the first query, only when its data is available. This ensures the query key and query function use the correct dependency value.

## Query State Transitions

The dependent query goes through these states:

```
(not created yet)         — first query is still loading
    ↓
Pending + Fetching        — first query completed, dependent query fetching
    ↓
Success + Idle            — dependent query loaded
```

Your application can inspect these states at any time to render the appropriate UI:

```csharp
if (userQuery.IsLoading)
{
    // Show user loading state
}
else if (userQuery.IsError)
{
    // Show error
}
else if (userQuery.IsSuccess)
{
    // User loaded — check dependent query
    if (projectsQuery == null || projectsQuery.IsLoading)
    {
        // Show projects loading state
    }
    else if (projectsQuery.IsSuccess)
    {
        // Both loaded — use projectsQuery.Data
    }
}
```

## Dependent Queries with UseQueries

When the first query returns a list of IDs, you can fan out into parallel dependent queries using `UseQueries`:

```csharp
var usersQuery = new UseQuery<List<string>>(
    new QueryOptions<List<string>>(
        queryKey: new("users"),
        queryFn: async ctx =>
        {
            var users = await GetUsersDataAsync();
            return users.Select(u => u.Id).ToList();
        }
    ),
    queryClient
);

UseQueries<List<Message>>? messagesQueries = null;

usersQuery.OnChange += () =>
{
    if (usersQuery.IsSuccess && usersQuery.Data != null)
    {
        messagesQueries?.Dispose();
        messagesQueries = new UseQueries<List<Message>>(queryClient);
        messagesQueries.OnChange += () =>
        {
            // Notify your UI framework to re-render
        };

        var queries = usersQuery.Data.Select(id =>
            new QueryOptions<List<Message>>(
                queryKey: new("messages", id),
                queryFn: async ctx => await GetMessagesByUserAsync(id, ctx.Signal)
            )
        );

        messagesQueries.SetQueries(queries);
        _ = messagesQueries.ExecuteAllAsync();
    }

    // Notify your UI framework to re-render
};

_ = usersQuery.ExecuteAsync();
```

If `usersQuery.Data` is null or empty, no dependent queries will be created.

## Multiple Dependencies (Chained)

For chains of 3+ queries, use the same reactive pattern with nested `OnChange` handlers:

```csharp
UseQuery<User>? userQuery = null;
UseQuery<Organization>? orgQuery = null;
UseQuery<Team>? teamQuery = null;

userQuery = new UseQuery<User>(
    new QueryOptions<User>(
        queryKey: new("user", userId),
        queryFn: async ctx => await GetUserAsync(userId)
    ),
    queryClient
);

userQuery.OnChange += () =>
{
    var orgId = userQuery.Data?.OrganizationId;
    if (userQuery.IsSuccess && !string.IsNullOrEmpty(orgId))
    {
        orgQuery?.Dispose();
        orgQuery = new UseQuery<Organization>(
            new QueryOptions<Organization>(
                queryKey: new("organization", orgId),
                queryFn: async ctx => await GetOrganizationAsync(orgId)
            ),
            queryClient
        );

        orgQuery.OnChange += () =>
        {
            var teamId = orgQuery.Data?.DefaultTeamId;
            if (orgQuery.IsSuccess && !string.IsNullOrEmpty(teamId))
            {
                teamQuery?.Dispose();
                teamQuery = new UseQuery<Team>(
                    new QueryOptions<Team>(
                        queryKey: new("team", teamId),
                        queryFn: async ctx => await GetTeamAsync(teamId)
                    ),
                    queryClient
                );

                teamQuery.OnChange += () => { /* notify UI */ };
                _ = teamQuery.ExecuteAsync();
            }

            // notify UI
        };

        _ = orgQuery.ExecuteAsync();
    }

    // notify UI
};

_ = userQuery.ExecuteAsync();

// Cleanup
// userQuery.Dispose(); orgQuery?.Dispose(); teamQuery?.Dispose();
```

Your application can render each stage incrementally by checking the state of each query:

```csharp
if (userQuery.IsLoading)
    // "Loading user..."
else if (userQuery.IsSuccess && orgQuery?.IsLoading == true)
    // "User: {name}, loading organization..."
else if (orgQuery?.IsSuccess == true && teamQuery?.IsLoading == true)
    // "Org: {name}, loading team..."
else if (teamQuery?.IsSuccess == true)
    // "Team: {name}" — all data ready
```

## Performance Note: Request Waterfalls

**Important**: Dependent queries create request waterfalls, which can hurt performance.

If both queries take the same amount of time, doing them serially instead of in parallel always takes **twice as much time**. This is especially problematic on high-latency connections.

### Better Alternative: Restructure Backend APIs

Instead of:
```
Client -> GetUserByEmail(email) -> GetProjectsByUser(userId)
```

Consider creating a combined endpoint:
```
Client -> GetProjectsByUserEmail(email)
```

This flattens the waterfall and improves performance significantly.

### When Dependent Queries Are Acceptable

Dependent queries are acceptable when:
- The dependency is truly required (can't be restructured)
- The queries are fast (low latency)
- The dependency is a local condition (not network data)
- User experience benefits from incremental loading