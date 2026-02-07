# GitHub Copilot Instructions for SwrSharp

## Project Overview
SwrSharp is a C# port of TanStack React Query for Blazor applications. It provides powerful asynchronous state management, caching, and data synchronization capabilities.

## Core Principles

### 1. React Query Compatibility
**CRITICAL**: SwrSharp aims to closely follow React Query's behavior and patterns.

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
**Matches React Query exactly:**
- SwrSharp: `retry: 3` = 3 retries after initial attempt (4 total attempts)
- React Query: `retry: 3` = 3 retries after initial (4 total attempts)
- ✅ Same behavior!

**Implementation details:**
- `attemptIndex` starts at 0 for first RETRY (not initial attempt)
- `retryDelayFunc` receives `attemptIndex` (0 = first retry)
- Default delay: `Math.Min(1000 * 2^attemptIndex, 30000)`
- No jitter in default (matches React Query)

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

---

## PR Review: React Query Feature Parity Checklist

When reviewing PRs, verify the implementation matches React Query behavior. Use the official docs as the source of truth: https://tanstack.com/query/latest/docs/framework/react/overview

### Query State & Status

| Feature | React Query | SwrSharp | Check |
|---------|-------------|-------------|-------|
| `status: 'pending'` | No data, no error | `Status == QueryStatus.Pending` | ✅ |
| `status: 'error'` | Has error (even with stale data) | `Status == QueryStatus.Error` | ✅ |
| `status: 'success'` | Has data, no error | `Status == QueryStatus.Success` | ✅ |
| `fetchStatus: 'fetching'` | Actively fetching | `FetchStatus == FetchStatus.Fetching` | ✅ |
| `fetchStatus: 'paused'` | Paused (offline) | `FetchStatus == FetchStatus.Paused` | ✅ |
| `fetchStatus: 'idle'` | Not fetching | `FetchStatus == FetchStatus.Idle` | ✅ |
| `isLoading` | `isPending && isFetching` | `IsLoading` | ✅ |
| `isPending` | status === 'pending' | `IsPending` | ✅ |
| `isSuccess` | status === 'success' | `IsSuccess` | ✅ |
| `isError` | status === 'error' | `IsError` | ✅ |
| `isFetching` | fetchStatus === 'fetching' | `IsFetching` | ✅ |
| `isPaused` | fetchStatus === 'paused' | `IsPaused` | ✅ |

### Retry Behavior

| Feature | React Query | SwrSharp | Check |
|---------|-------------|-------------|-------|
| `retry: false` | No retries | `Retry = 0` or not set | ✅ |
| `retry: 3` | 3 retries (4 total attempts) | `Retry = 3` → 3 retries | ✅ |
| `retry: true` | Infinite retries | `RetryInfinite = true` | ✅ |
| `retry: (count, err) => bool` | Custom logic | `RetryFunc` | ✅ |
| `failureCount` | Starts at 0 | `FailureCount` | ✅ |
| `failureReason` | Error during retries | `FailureReason` | ✅ |
| Default delay | `1000 * 2^attemptIndex` | Same formula | ✅ |
| Max delay | 30 seconds | `MaxRetryDelay` | ✅ |
| `retryDelay: (attempt) => ms` | Custom delay | `RetryDelayFunc` | ✅ |

### Network Mode Behavior

| Feature | React Query | SwrSharp | Check |
|---------|-------------|-------------|-------|
| Online mode - offline | Pauses fetch | Sets `FetchStatus.Paused` | ✅ |
| Online mode - reconnect | Continues/refetches | Continues from pause | ✅ |
| Always mode | Ignores network | Never pauses | ✅ |
| OfflineFirst mode | First fetch proceeds | Then pauses if offline | ✅ |
| Pause during fetch | Cancels and pauses | `_currentCts.Cancel()` | ✅ |
| Pause during retry | Pauses retry loop | Semaphore wait | ✅ |
| Continue vs refetch | Continue from pause | Not a new fetch | ✅ |
| Cancelled while paused | Won't continue | Checks `signal.IsCancellationRequested` | ✅ |

### Stale Time & Caching

| Feature | React Query | SwrSharp | Check |
|---------|-------------|-------------|-------|
| `staleTime: 0` | Always stale | Default behavior | ✅ |
| `staleTime: Infinity` | Never stale | Large TimeSpan | ✅ |
| Background refetch | Refetch stale data | `IsFetchingBackground` | ✅ |
| Cache sharing | Shared by queryKey | `QueryClient` cache | ✅ |

### Refetch Behavior

| Feature | React Query | SwrSharp | Check |
|---------|-------------|-------------|-------|
| `refetchOnReconnect` | Refetch on network return | `RefetchOnReconnect` | ✅ |
| `refetchInterval` | Polling interval | `RefetchInterval` | ✅ |
| `refetchIntervalInBackground` | Continue when tab hidden | ❌ Not implemented | ⚠️ |

### Query Function Context

| Feature | React Query | SwrSharp | Check |
|---------|-------------|-------------|-------|
| `queryKey` | Array of keys | `QueryKey` | ✅ |
| `signal` | AbortSignal | `CancellationToken` | ✅ |
| `meta` | Optional metadata | `Meta` | ✅ |
| Destructuring | `({ queryKey, signal })` | `var (key, signal) = ctx` | ✅ |

### PR Review Questions

When reviewing a PR, ask these questions:

1. **Does it match React Query?**
   - Check the corresponding React Query docs page
   - Verify property names, behavior, and edge cases
   - Note any intentional deviations with justification

2. **Is the implementation complete?**
   - All related properties exposed (e.g., `FailureReason` with `FailureCount`)
   - Appropriate events/notifications fired on state changes
   - Edge cases handled (offline, cancelled, disposed)

3. **Are tests comprehensive?**
   - Test matches React Query expected behavior
   - Edge cases covered (offline mid-fetch, cancel while paused)
   - No flaky tests

4. **Is documentation updated?**
   - Markdown docs reflect new behavior
   - Code comments explain React Query compatibility
   - Deviations documented with reasons

5. **Is it thread-safe?**
   - No `new Random()` (use `Random.Shared`)
   - Async locks use `SemaphoreSlim`
   - Shared state properly protected

### Known Deviations from React Query

Document any intentional differences here:

| Feature | React Query | SwrSharp | Reason |
|---------|-------------|-------------|--------|
| `refetchIntervalInBackground` | Pauses when tab hidden | Not implemented | Blazor doesn't have native tab visibility API; requires JS interop |
| Suspense support | Built-in | Not applicable | C# doesn't have Suspense pattern |
| SSR hydration | Supported | Different approach | Blazor Server has different lifecycle |

### Quick Reference: React Query Docs Links

- [Query Basics](https://tanstack.com/query/latest/docs/framework/react/guides/queries)
- [Query Keys](https://tanstack.com/query/latest/docs/framework/react/guides/query-keys)
- [Query Functions](https://tanstack.com/query/latest/docs/framework/react/guides/query-functions)
- [Network Mode](https://tanstack.com/query/latest/docs/framework/react/guides/network-mode)
- [Query Retries](https://tanstack.com/query/latest/docs/framework/react/guides/query-retries)
- [Caching](https://tanstack.com/query/latest/docs/framework/react/guides/caching)
- [Mutations](https://tanstack.com/query/latest/docs/framework/react/guides/mutations)

## References

- React Query Docs: https://tanstack.com/query/latest/docs/framework/react/overview
- SwrSharp Docs: See markdown files in root directory
- Fixes History: See `FIXES_APPLIED.md`

## Questions?

When in doubt:
1. Check React Query documentation
2. Look at existing SwrSharp patterns
3. Review test cases for expected behavior
4. Ask for clarification before making assumptions

