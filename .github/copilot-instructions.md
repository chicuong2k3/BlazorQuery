# GitHub Copilot Instructions for BlazorQuery

## Project Overview
BlazorQuery is a C# port of TanStack React Query for Blazor applications. It provides powerful asynchronous state management, caching, and data synchronization capabilities.

## Core Principles

### 1. React Query Compatibility
**CRITICAL**: BlazorQuery aims to closely follow React Query's behavior and patterns.

When implementing features or making changes:
1. **Always check React Query documentation first** at https://tanstack.com/query/latest/docs/framework/react/overview
2. Understand how React Query implements the feature
3. Adapt the implementation to C#/.NET idioms while maintaining the same behavior
4. Document any intentional deviations from React Query

### 2. State Management Rules

#### Query Status (QueryStatus enum)
```csharp
// React Query logic - follow this EXACTLY:
// - error: has error (even if has stale data)
// - pending: no data AND no error  
// - success: has data AND no error

public QueryStatus Status
{
    get
    {
        if (Error != null)
            return QueryStatus.Error;
        if (Data == null)
            return QueryStatus.Pending;
        return QueryStatus.Success;
    }
}
```

#### Loading States
```csharp
// React Query: isLoading = isPending && isFetching
public bool IsLoading => Status == QueryStatus.Pending && 
                         (FetchStatus == FetchStatus.Fetching || FetchStatus == FetchStatus.Paused);
```

Key points:
- `IsLoading` means "first load in progress" (no data yet)
- `IsFetching` means "actively fetching" (can be background refetch)
- `IsPaused` means "paused due to network conditions"
- Status takes priority: error state overrides success even with stale data

### 3. Thread Safety
**ALWAYS** ensure thread-safe code:
- Use `Random.Shared` instead of `new Random()` (not thread-safe)
- Protect shared state with locks or concurrent collections
- Consider async/await patterns carefully
- Use `SemaphoreSlim` for async locking, not `lock`

### 4. Network Modes
Implement three network modes:
- **Online**: Only fetch when online, pause when offline
- **Always**: Always fetch regardless of network state
- **OfflineFirst**: Try once, then pause if offline

### 5. Retry Logic
**Important deviation from React Query:**
- BlazorQuery: `retry: 3` = max 3 total attempts (initial + 2 retries)
- React Query: `retry: 3` = 3 retries after initial (4 total attempts)

This is an intentional design choice for simplicity. Document this clearly.

### 6. Documentation Requirements

**CRITICAL**: When changing code, ALWAYS update documentation:

1. **Update relevant .md files** in the root directory:
   - `1. Query Keys.md`
   - `2. Query Functions.md`
   - `3. Network Mode.md`
   - `4. Query Retries.md`

2. **Keep documentation in sync** with implementation:
   - Update code examples
   - Fix incorrect descriptions
   - Add notes about deviations from React Query
   - Include why decisions were made

3. **Document fixes** in `FIXES_APPLIED.md` when making corrections

### 7. Testing

Always ensure:
- All tests pass (`dotnet test`)
- No flaky tests (run multiple times)
- New features have corresponding tests
- Tests follow React Query's expected behavior

### 8. Code Style

Follow these patterns:
```csharp
// Good: Clear intent with comments
public QueryStatus Status
{
    get
    {
        // React Query logic: error > pending > success
        if (Error != null)
            return QueryStatus.Error;
        if (Data == null)
            return QueryStatus.Pending;
        return QueryStatus.Success;
    }
}

// Good: Thread-safe random
double jitter = Random.Shared.NextDouble() * 300;

// Good: Async locking
private readonly SemaphoreSlim _fetchLock = new(1, 1);
await _fetchLock.WaitAsync();
try 
{
    // ... critical section
}
finally
{
    _fetchLock.Release();
}

// Good: Context destructuring (JavaScript-like)
queryFn: async ctx => {
    var (queryKey, signal) = ctx;
    var id = (int)queryKey[1]!;
    return await FetchDataAsync(id, signal);
}
```

### 9. Common Pitfalls to Avoid

❌ **Don't:**
- Use `new Random()` - not thread-safe
- Use `lock` with async code - use `SemaphoreSlim`
- Forget to update docs after code changes
- Deviate from React Query without good reason
- Assume behavior - verify against React Query docs

✅ **Do:**
- Check React Query docs before implementing
- Use `Random.Shared` for thread safety
- Update all related documentation
- Add comments explaining React Query compatibility
- Write tests that verify React Query behavior
- Document intentional deviations

### 10. Review Checklist

Before committing changes, verify:
- [ ] Checked React Query documentation
- [ ] Implementation matches React Query behavior (or deviation is documented)
- [ ] All tests pass
- [ ] Documentation updated
- [ ] Code is thread-safe
- [ ] Comments explain complex logic
- [ ] No breaking changes (or documented)

## References

- React Query Docs: https://tanstack.com/query/latest/docs/framework/react/overview
- BlazorQuery Docs: See markdown files in root directory
- Fixes History: See `FIXES_APPLIED.md`

## Questions?

When in doubt:
1. Check React Query documentation
2. Look at existing BlazorQuery patterns
3. Review test cases for expected behavior
4. Ask for clarification before making assumptions

