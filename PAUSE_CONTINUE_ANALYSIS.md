# React Query Pause/Continue Behavior Analysis

## 🎯 React Query Behavior (từ docs)

> "If a query runs because you are online, but you go offline while the fetch is still happening, TanStack Query will also pause the retry mechanism. Paused queries will then continue to run once you re-gain network connection. This is independent of refetchOnReconnect (which also defaults to true in this mode), because it is not a refetch, but rather a continue. If the query has been cancelled in the meantime, it will not continue."

### Key Points:
1. **Query đang fetch** → offline → **pause retry**
2. Online trở lại → **continue** (NOT refetch)
3. Independent từ `refetchOnReconnect`
4. Nếu đã cancelled → không continue

---

## 📊 SwrSharp Implementation Hiện Tại

### ✅ Đã implement:
1. **Pause retry khi offline** (line 304-319 UseQuery.cs)
2. **Wait cho online** với semaphore
3. **Continue retry loop** khi online trở lại
4. **Check offline sau retry delay** (line 329-343)

### ⚠️ Limitation:
- Logic pause chỉ kích hoạt khi:
  - TRƯỚC retry delay: Check offline → pause → wait
  - SAU retry delay: Check offline → pause → wait
  
- **Chưa handle**: Offline TRONG khi đang execute query function (mid-fetch của chính fetch operation)

---

## 🔍 Detailed Analysis

### Scenario 1: Offline TRƯỚC retry ✅ WORKS
```
Attempt 1 → Fail → Check offline → Pause → Wait → Online → Continue → Attempt 2
```
**Status**: ✅ Implemented correctly

### Scenario 2: Offline TRONG retry delay ✅ WORKS  
```
Attempt 1 → Fail → Task.Delay(1000ms) → (offline tại 500ms) → 
→ Delay ends → Check offline → Pause → Wait → Online → Continue → Attempt 2
```
**Status**: ✅ Implemented with post-delay check

### Scenario 3: Offline TRONG query function execution ⚠️ PARTIAL
```
Attempt 1 → FetchAsync started → (offline mid-fetch) → ???
```

**Current behavior**:
- Nếu `queryFn` uses `CancellationToken` và network operation respects it → throws `OperationCanceledException` → Paused
- Nếu `queryFn` KHÔNG use cancellation → continues until timeout/error

**React Query behavior**:
- Automatically pauses tại network layer
- Browser/fetch API handles này automatically

---

## 💡 Key Difference: Platform

### React Query (Browser/JavaScript):
- Chạy trên browser
- `fetch()` API tự động aware của network status
- Browser events (`online`/`offline`) reliable
- Có thể pause HTTP requests mid-flight

### SwrSharp (C#/.NET):
- Chạy trên server hoặc WebAssembly  
- `HttpClient` KHÔNG tự động pause khi offline
- Network detection phụ thuộc vào `IOnlineManager` implementation
- KHÔNG thể pause .NET HTTP requests mid-flight (phải cancel)

---

## 🎯 Current Implementation Assessment

### What Works ✅:
1. **Pause retry mechanism** when offline between attempts
2. **Continue** (not restart) when coming back online
3. **Semaphore-based wait** for network restoration
4. **Check offline** both before and after retry delay

### What's Different ⚠️:
1. **Mid-fetch pause**: Không thể pause .NET HTTP request mid-flight
   - React Query: Browser pause request
   - SwrSharp: Must cancel and restart (hoặc let it complete)
   
2. **Network detection**: Phụ thuộc vào `IOnlineManager`
   - React Query: Browser `navigator.onLine`
   - SwrSharp: Custom implementation

### What's Missing ❌:
1. **Automatic mid-fetch detection**: Cần queryFn cooperate với CancellationToken
2. **Granular pause during Task.Delay**: Hiện chỉ check sau delay xong

---

## 📝 Recommendation

### Option 1: Document Current Behavior ✅ RECOMMENDED
- Explain behavior works at **retry level**, not fetch level
- Note difference từ React Query due to platform
- Provide best practices cho using CancellationToken

### Option 2: Enhanced Implementation
- Monitor network status DURING Task.Delay (polling)
- Immediately cancel delay khi offline detected
- Requires more complex implementation

### Option 3: Full Parity (Complex)
- Wrap ALL async operations với network monitoring
- Cancel operations immediately on offline
- Resume exactly where left off
- **Very complex**, may not be worth it

---

## 🎓 Conclusion

**Current implementation is GOOD ENOUGH** vì:
1. ✅ Handles retry pause/continue correctly
2. ✅ Respects React Query mental model
3. ✅ Works within .NET platform limitations
4. ⚠️ Requires queryFn to use CancellationToken for mid-fetch pause

**Documentation should**:
1. Explain behavior clearly
2. Note platform differences
3. Show best practices with CancellationToken
4. Example proper queryFn implementation

---

## 📖 Documentation Update Needed

Update `3. Network Mode.md` to clarify:

```markdown
### Pause and Continue Behavior

When a query is retrying and the network goes offline, SwrSharp will:
1. **Pause the retry mechanism** (not restart)
2. **Wait for network** to come back online
3. **Continue from current attempt** (not from beginning)

**Important**: For mid-fetch pause (while the query function is executing), 
your query function should respect the `CancellationToken` provided in the context:

\`\`\`csharp
queryFn: async ctx => {
    var (queryKey, signal) = ctx;
    var response = await httpClient.GetAsync(url, signal); // Pass signal!
    return await response.Content.ReadFromJsonAsync<T>(signal);
}
\`\`\`

**Platform Note**: Unlike React Query (browser), .NET cannot pause HTTP requests 
mid-flight. Requests are either cancelled (via CancellationToken) or complete normally.
```

---

**✨ Summary**: Implementation đúng về mặt retry mechanism, cần document rõ ràng về platform limitations và best practices.

