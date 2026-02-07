# Query Options Pattern - Documentation Summary

## 🎯 Overview

Đã bổ sung documentation về **Reusable Query Options Pattern**, tương tự như `queryOptions` helper trong React Query nhưng adapted cho C# idioms.

---

## ✨ Key Insight

**Không cần helper function như React Query** vì:
1. `QueryOptions<T>` constructor đã cung cấp full type safety
2. Factory methods (static methods) cho chúng ta reusability
3. Target-typed `new()` expressions giữ code concise
4. C# type system mạnh hơn TypeScript nên không cần wrapper

---

## 📚 Documentation Created

### File: `5. Query Options.md` (MỚI)

Comprehensive guide covering:

#### 1. **Basic Pattern**
```csharp
// Define reusable factory
static QueryOptions<Group> GroupOptions(int id)
{
    return new QueryOptions<Group>(
        queryKey: new("groups", id),
        queryFn: async ctx => await FetchGroupAsync(id),
        staleTime: TimeSpan.FromSeconds(5)
    );
}

// Use everywhere
var query = new UseQuery<Group>(GroupOptions(1), queryClient);
```

#### 2. **Benefits**
- Co-location of query key and function
- Type safety
- Reusability
- Consistency
- Easy refactoring

#### 3. **Advanced Examples**
- Multiple parameters
- With metadata
- Service/Repository pattern
- Overriding options

#### 4. **Best Practices**
- DO's and DON'Ts
- Naming conventions
- Organization patterns

#### 5. **React Query Comparison**
Side-by-side comparison showing how C# pattern achieves the same goals

---

## 📝 Files Updated

### 1. **`README.md`**
- ✅ Added "Query Options" to documentation links
- ✅ Added "Reusable Query Options" example section
- ✅ Shows factory method pattern in quick start

### 2. **`tests/BlazorQuery.Core.Tests/UseQueryTests.cs`**
- ✅ Added test `ReusableQueryOptions_WorksCorrectly`
- ✅ Demonstrates factory method pattern
- ✅ Verifies it works with multiple instances

### 3. **`5. Query Options.md`** (NEW)
- ✅ Complete documentation
- ✅ Multiple examples
- ✅ Best practices
- ✅ Comparison with React Query

---

## 🆚 React Query vs BlazorQuery

### React Query (TypeScript):
```typescript
import { queryOptions } from '@tanstack/react-query'

function groupOptions(id: number) {
  return queryOptions({
    queryKey: ['groups', id],
    queryFn: () => fetchGroups(id),
    staleTime: 5 * 1000,
  })
}

useQuery(groupOptions(1))
```

### BlazorQuery (C#):
```csharp
// No import needed - just use QueryOptions directly

static QueryOptions<Group> GroupOptions(int id)
{
    return new QueryOptions<Group>(
        queryKey: new("groups", id),
        queryFn: async ctx => await FetchGroupsAsync(id),
        staleTime: TimeSpan.FromSeconds(5)
    );
}

var query = new UseQuery<Group>(GroupOptions(1), queryClient);
```

**Same pattern, native C# idioms!**

---

## 💡 Why This Approach?

### React Query needs `queryOptions()` helper because:
- TypeScript type inference limitations
- Need explicit return type annotation
- Helper provides better IntelliSense

### BlazorQuery doesn't need it because:
- ✅ C# has better type inference
- ✅ `QueryOptions<T>` constructor is already typed
- ✅ Factory methods are idiomatic C#
- ✅ Target-typed `new()` keeps it concise

---

## 📊 Code Examples

### Simple Factory:
```csharp
static QueryOptions<Todo> TodoOptions(int id) =>
    new(
        queryKey: new("todo", id),
        queryFn: async ctx => await FetchTodoAsync(id)
    );
```

### With Service Pattern:
```csharp
public class TodoQueryFactory
{
    private readonly ITodoApi _api;

    public TodoQueryFactory(ITodoApi api) => _api = api;

    public QueryOptions<List<Todo>> List(string? status = null) =>
        new(
            queryKey: new("todos", "list", status ?? "all"),
            queryFn: async ctx => await _api.GetTodosAsync(status, ctx.Signal),
            staleTime: TimeSpan.FromMinutes(5)
        );

    public QueryOptions<Todo> ById(int id) =>
        new(
            queryKey: new("todos", "detail", id),
            queryFn: async ctx => await _api.GetTodoByIdAsync(id, ctx.Signal),
            staleTime: TimeSpan.FromMinutes(10)
        );
}
```

---

## ✅ Test Results

```
✅ 43/43 tests PASS
✅ New test validates factory pattern
✅ Pattern works correctly with multiple instances
✅ Full type safety maintained
```

---

## 🎯 Benefits for Developers

### Before (inline):
```csharp
// Component 1
var query = new UseQuery<Group>(
    new QueryOptions<Group>(
        queryKey: new("groups", 1),
        queryFn: async ctx => await FetchGroupAsync(1),
        staleTime: TimeSpan.FromSeconds(5)
    ),
    queryClient
);

// Component 2
var query = new UseQuery<Group>(
    new QueryOptions<Group>(
        queryKey: new("groups", 2),
        queryFn: async ctx => await FetchGroupAsync(2),
        staleTime: TimeSpan.FromSeconds(5)
    ),
    queryClient
);
```
**Problems:**
- ❌ Duplication
- ❌ Hard to maintain
- ❌ Inconsistent configuration

### After (factory):
```csharp
// Define once
static QueryOptions<Group> GroupOptions(int id) =>
    new(
        queryKey: new("groups", id),
        queryFn: async ctx => await FetchGroupAsync(id),
        staleTime: TimeSpan.FromSeconds(5)
    );

// Use everywhere
var query1 = new UseQuery<Group>(GroupOptions(1), queryClient);
var query2 = new UseQuery<Group>(GroupOptions(2), queryClient);
```
**Benefits:**
- ✅ DRY (Don't Repeat Yourself)
- ✅ Easy to maintain
- ✅ Consistent configuration
- ✅ Easy refactoring

---

## 📖 Documentation Structure

```
5. Query Options.md
├── Overview
├── Basic Pattern
├── Benefits (5 points)
├── Advanced Examples
│   ├── Multiple Parameters
│   ├── With Metadata
│   ├── Service/Repository Pattern
│   └── Overriding Options
├── Best Practices
│   ├── DO's
│   └── DON'Ts
├── Comparison with React Query
└── Summary
```

---

## 🎨 Code Style Consistency

Document follows established patterns:
- ✅ Target-typed `new()` expressions
- ✅ Destructuring context: `var (queryKey, signal) = ctx`
- ✅ Multi-line formatting
- ✅ Clear comments
- ✅ Practical examples

---

## 🚀 Impact

### Developer Experience:
- ⭐⭐⭐⭐⭐ **Excellent organization**
- 📦 **Better code structure** with factories
- 🔄 **Easy to refactor** - change once, update everywhere
- 🎯 **Clear separation** of concerns

### Code Quality:
- ✅ **DRY principle** - no duplication
- ✅ **Single source of truth** for each query
- ✅ **Type-safe** throughout
- ✅ **Testable** - factory methods easy to test

### Migration from React Query:
- ✅ **Familiar pattern** for React Query developers
- ✅ **Same mental model** different syntax
- ✅ **Easy to understand** documentation with comparisons

---

## 🎓 Key Takeaway

> **C# doesn't need a special `queryOptions()` helper because the language features (type inference, factory methods, target-typed new) already provide the same benefits naturally.**

Pattern tương tự React Query nhưng sử dụng native C# idioms!

---

**✨ BlazorQuery giờ đây có documentation đầy đủ về Reusable Query Options pattern, giúp developers organize code tốt hơn và maintain dễ dàng hơn!**

