# Disabling/Pausing Queries - Documentation Complete ✅

## 🎯 Feature: Disabling/Pausing Queries

Comprehensive documentation about using `enabled` option to control when queries execute, including lazy queries and best practices.

---

## ✅ Documentation Created

### **File**: `9. Disabling Queries.md`

Complete guide covering all aspects of disabling and pausing queries in SwrSharp.

---

## 📚 Content Covered

### 1. **Basic Usage** ✅
- How to use `enabled: false`
- Behavior when disabled
- States with/without cached data

### 2. **Why Permanent Disabling is Not Recommended** ✅
- Declarative vs imperative approaches
- Missing out on background refetches
- When it's appropriate vs not

### 3. **Lazy Queries** ✅
- Conditional enabling based on state
- Filter form example
- Dynamic enable/disable

### 4. **IsLoading for Lazy Queries** ✅
- Distinction between `IsPending` and `IsLoading`
- Why `IsLoading` is better for disabled queries
- Complete state explanation

### 5. **Behavior with Cached Data** ✅
- Query with cached data starts in Success state
- Query without cache starts in Pending + Idle state
- Cache remains available when disabled

### 6. **Ignore Invalidations When Disabled** ✅
- Disabled queries ignore `Invalidate()` calls
- Manual refetch requires enabling first
- Example workflow

### 7. **Complete Examples** ✅
- Manual data loading component
- Filter-based lazy loading
- Dynamic mode switching
- User dashboard with conditional loading

### 8. **Best Practices** ✅
- Prefer lazy queries over permanent disabling
- Use for conditional data loading
- Handle loading states properly
- When to use each approach

### 9. **React Query Comparison** ✅
- Side-by-side code comparison
- Key differences explained
- Platform adaptations

### 10. **Note on skipToken** ✅
- Explanation why SwrSharp doesn't need it
- C# alternatives with nullable types
- Type-safe conditional queries

---

## 🎯 Key Points Documented

### When `enabled: false`:

1. **With Cached Data**:
   ```
   Status: Success
   FetchStatus: Idle
   Data: <cached data>
   ```

2. **Without Cached Data**:
   ```
   Status: Pending
   FetchStatus: Idle
   Data: null
   IsLoading: false (not fetching!)
   ```

3. **Behaviors**:
   - ❌ Won't fetch on mount
   - ❌ Won't background refetch
   - ❌ Ignores invalidations
   - ✅ Can manually refetch (after enabling)

---

## 💡 Usage Examples

### Lazy Query (Recommended):
```csharp
var query = new UseQuery<List<Todo>>(
    new QueryOptions<List<Todo>>(
        queryKey: new("todos", filter),
        queryFn: async ctx => await FetchTodosAsync(filter),
        enabled: !string.IsNullOrEmpty(filter) // Conditional
    ),
    queryClient
);

// Query auto-fetches when filter is set
```

### Manual Fetch (Not Recommended):
```csharp
var query = new UseQuery<Data>(
    new QueryOptions<Data>(
        queryKey: new("data"),
        queryFn: async ctx => await FetchDataAsync(),
        enabled: false // Always disabled
    ),
    queryClient
);

// Must manually trigger
query._queryOptions.Enabled = true;
await query.RefetchAsync();
```

### Dynamic Enable/Disable:
```csharp
// Disable
query._queryOptions.Enabled = false;

// Enable and fetch
query._queryOptions.Enabled = true;
await query.ExecuteAsync();
```

---

## 🆚 vs Dependent Queries

### Disabling Queries (this doc):
- **Purpose**: Control WHETHER query executes
- **Use case**: Filters, conditional features, manual loading
- **Pattern**: `enabled: condition`

### Dependent Queries (doc 7):
- **Purpose**: Control WHEN query executes (ordering)
- **Use case**: Query chains (User → Projects → Tasks)
- **Pattern**: `enabled: dependencyIsReady`

**Both use same `enabled` option, different purposes!**

---

## 📊 React Query Parity

| Feature | React Query | SwrSharp | Status |
|---------|-------------|-------------|--------|
| `enabled` option | ✓ | ✓ | ✅ Same |
| Disabled with cache = Success | ✓ | ✓ | ✅ Same |
| Disabled no cache = Pending + Idle | ✓ | ✓ | ✅ Same |
| Ignore invalidations | ✓ | ✓ | ✅ Same |
| `isLoading` for disabled | ✓ | `IsLoading` | ✅ Same |
| `skipToken` | ✓ | N/A (not needed) | ✅ C# has nullable types |
| Manual refetch | ✓ `refetch()` | `RefetchAsync()` after enabling | ✅ Equivalent |

---

## 📁 Files Created/Updated

### Documentation:
1. ✅ `9. Disabling Queries.md` - Complete guide (400+ lines)
2. ✅ `README.md` - Added link

---

## ✨ Documentation Highlights

### 1. **Comprehensive Coverage** ✅
- All React Query behaviors documented
- C# equivalents provided
- Multiple real-world examples

### 2. **Clear Explanations** ✅
- When to use vs not use
- State transitions explained
- Best practices highlighted

### 3. **Practical Examples** ✅
- Filter forms
- Manual loaders
- Conditional features
- Complete components

### 4. **Warnings & Recommendations** ✅
- Why permanent disabling is bad
- Prefer lazy queries
- Handle states properly

### 5. **React Query Comparison** ✅
- Side-by-side code
- Platform differences
- Same mental model

---

## 🎓 Key Takeaways

### Do's ✅:
- ✅ Use for lazy queries (conditional enable)
- ✅ Use for filters and search
- ✅ Use `IsLoading` not `IsPending`
- ✅ Enable before manual refetch

### Don'ts ❌:
- ❌ Don't permanently disable if you want background updates
- ❌ Don't use `IsPending` to show spinner on disabled query
- ❌ Don't forget to enable before refetch
- ❌ Don't overuse imperative fetching

---

## 📖 Related Documentation

- **Dependent Queries** (Doc 7) - Query chains with `enabled`
- **Query Options** (Doc 5) - Reusable query configurations
- **Background Fetching** (Doc 8) - Loading indicators

---

## 🎯 Summary

**Documentation Status**: ✅ **Complete**

- ✅ Comprehensive guide written (400+ lines)
- ✅ All React Query behaviors covered
- ✅ Multiple practical examples
- ✅ Best practices explained
- ✅ React Query comparison provided
- ✅ C# idioms adapted
- ✅ Warnings and recommendations included
- ✅ Related docs cross-referenced

**Developer Experience**: ⭐⭐⭐⭐⭐

Developers can now:
- Understand when to disable queries
- Implement lazy queries correctly
- Use conditional data loading
- Handle loading states properly
- Follow React Query patterns

**Completes Feature Set**: 

SwrSharp now has complete documentation for:
1. Query Keys ✅
2. Query Functions ✅
3. Network Mode ✅
4. Query Retries ✅
5. Query Options ✅
6. Parallel Queries ✅
7. Dependent Queries ✅
8. Background Fetching Indicators ✅
9. Disabling Queries ✅

**🎉 Documentation suite is comprehensive and production-ready!**

