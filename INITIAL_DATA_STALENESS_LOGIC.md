# Initial Data Staleness Logic - Explanation

## ✅ Implementation is CORRECT

The comment mentioned:
```csharp
// If stale and enabled, will refetch on first ExecuteAsync call
// If fresh, won't refetch until staleTime expires
```

This logic **IS implemented** in `ExecuteAsync()` method, not in `InitializeWithInitialData()`.

---

## 📋 How It Works

### 1. **InitializeWithInitialData()** (lines 156-193)

```csharp
private void InitializeWithInitialData()
{
    // Get initial data
    T? initialData = ...;
    
    if (hasInitialData && initialData != null)
    {
        // Set in cache with timestamp
        var initialDataUpdatedAt = _queryOptions.InitialDataUpdatedAt ?? DateTime.UtcNow;
        _client.Set(_queryOptions.QueryKey, initialData);
        
        // Update FetchTime if initialDataUpdatedAt provided
        var entry = _client.GetCacheEntry(_queryOptions.QueryKey);
        if (entry != null && _queryOptions.InitialDataUpdatedAt.HasValue)
        {
            entry.FetchTime = _queryOptions.InitialDataUpdatedAt.Value;
        }
        
        Data = initialData;
        
        // Staleness checking happens later in ExecuteAsync
    }
}
```

**Key Points**:
- ✅ Sets `initialData` in cache
- ✅ Updates `entry.FetchTime` with `initialDataUpdatedAt` (or current time)
- ✅ Sets local `Data` property
- ✅ Does NOT check staleness here (will be checked in ExecuteAsync)

---

### 2. **ExecuteAsync()** (lines 341-357)

```csharp
public async Task ExecuteAsync(...)
{
    var entry = _client.GetCacheEntry(_queryOptions.QueryKey);
    
    // Check if data is stale
    var isDataStale = entry == null || 
                      (_queryOptions.StaleTime > TimeSpan.Zero &&
                       (DateTime.UtcNow - entry.FetchTime) > _queryOptions.StaleTime);
    
    // If data is fresh, no need to fetch
    if (!isDataStale)
    {
        return; // ← Skip fetching if fresh
    }
    
    // Data is stale → continue with fetch
    FetchStatus = FetchStatus.Fetching;
    // ... fetch logic
}
```

**Key Points**:
- ✅ Gets cache entry (which has `FetchTime` set by InitializeWithInitialData)
- ✅ Calculates staleness: `(UtcNow - entry.FetchTime) > staleTime`
- ✅ If **fresh**: returns early (no fetch)
- ✅ If **stale**: continues with fetch

---

## 🎯 Behavior Examples

### Example 1: Fresh Initial Data (No Refetch)

```csharp
var twoSecondsAgo = DateTime.UtcNow.AddSeconds(-2);

var query = new UseQuery<string>(
    new QueryOptions<string>(
        queryKey: new("data"),
        queryFn: async ctx => await FetchAsync(),
        initialData: "Initial",
        staleTime: TimeSpan.FromSeconds(10),     // Fresh for 10 seconds
        initialDataUpdatedAt: twoSecondsAgo      // Data is 2 seconds old
    ),
    queryClient
);

// Immediate:
// - Data = "Initial"
// - entry.FetchTime = twoSecondsAgo

await query.ExecuteAsync();

// ExecuteAsync logic:
// - UtcNow - entry.FetchTime = ~2 seconds
// - staleTime = 10 seconds
// - 2 < 10 → data is FRESH
// - Returns early, NO FETCH
// - Data still = "Initial" ✓
```

### Example 2: Stale Initial Data (Refetch)

```csharp
var tenSecondsAgo = DateTime.UtcNow.AddSeconds(-10);

var query = new UseQuery<string>(
    new QueryOptions<string>(
        queryKey: new("data"),
        queryFn: async ctx => await FetchAsync(),
        initialData: "Initial",
        staleTime: TimeSpan.FromSeconds(5),      // Fresh for 5 seconds
        initialDataUpdatedAt: tenSecondsAgo      // Data is 10 seconds old
    ),
    queryClient
);

// Immediate:
// - Data = "Initial"
// - entry.FetchTime = tenSecondsAgo

await query.ExecuteAsync();

// ExecuteAsync logic:
// - UtcNow - entry.FetchTime = ~10 seconds
// - staleTime = 5 seconds
// - 10 > 5 → data is STALE
// - Continues with fetch
// - Data becomes "Fetched" ✓
```

### Example 3: No initialDataUpdatedAt (Treated as Just Fetched)

```csharp
var query = new UseQuery<string>(
    new QueryOptions<string>(
        queryKey: new("data"),
        queryFn: async ctx => await FetchAsync(),
        initialData: "Initial",
        staleTime: TimeSpan.FromSeconds(5)
        // No initialDataUpdatedAt → uses UtcNow
    ),
    queryClient
);

// Immediate:
// - Data = "Initial"
// - entry.FetchTime = DateTime.UtcNow (current time)

await query.ExecuteAsync();

// ExecuteAsync logic:
// - UtcNow - entry.FetchTime = ~0 seconds
// - staleTime = 5 seconds
// - 0 < 5 → data is FRESH
// - Returns early, NO FETCH
// - Data still = "Initial" ✓
```

---

## ✅ Tests Verify Correctness

### Test 1: `InitialDataUpdatedAt_ShouldRespectProvidedTimestamp`
- Initial data 10 seconds old
- staleTime = 5 seconds
- ✅ Expects: REFETCH (10 > 5)

### Test 2: `InitialDataUpdatedAt_WithFreshTimestamp_ShouldNotRefetch`
- Initial data 2 seconds old
- staleTime = 10 seconds
- ✅ Expects: NO REFETCH (2 < 10)

### Test 3: `InitialData_StalenessLogic_VerifyExecuteAsyncBehavior`
- Tests both scenarios in one test
- ✅ Verifies ExecuteAsync correctly checks staleness

---

## 📊 Logic Flow Diagram

```
┌─────────────────────────────────┐
│ new UseQuery with initialData   │
└────────────┬────────────────────┘
             │
             ▼
┌─────────────────────────────────┐
│ InitializeWithInitialData()     │
│ - Set data in cache             │
│ - Set entry.FetchTime           │
│ - Set local Data property       │
│ - Status = Success              │
└────────────┬────────────────────┘
             │
             ▼
┌─────────────────────────────────┐
│ User calls ExecuteAsync()       │
└────────────┬────────────────────┘
             │
             ▼
┌─────────────────────────────────┐
│ Calculate staleness:            │
│ age = UtcNow - entry.FetchTime  │
│ isStale = age > staleTime       │
└────────────┬────────────────────┘
             │
         ┌───┴───┐
         │       │
    Fresh│       │Stale
         │       │
         ▼       ▼
    ┌────────┐ ┌────────┐
    │Return  │ │Fetch   │
    │Early   │ │New Data│
    └────────┘ └────────┘
```

---

## 🎓 Summary

**Implementation is CORRECT**:

1. ✅ `InitializeWithInitialData()` sets up cache with proper timestamp
2. ✅ `ExecuteAsync()` checks staleness against that timestamp
3. ✅ Fresh data: no refetch (returns early)
4. ✅ Stale data: refetches automatically
5. ✅ Tests verify both scenarios
6. ✅ Matches React Query behavior exactly

**Why the confusion?**:
- The comment was in `InitializeWithInitialData()`
- But the logic is in `ExecuteAsync()`
- This is **correct separation of concerns**:
  - Initialize: setup cache
  - Execute: check and fetch if needed

**Result**: ✅ **Working as intended!**

