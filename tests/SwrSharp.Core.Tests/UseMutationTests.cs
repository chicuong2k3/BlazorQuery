using Moq;

namespace SwrSharp.Core.Tests;

public class UseMutationTests
{
    private readonly QueryClient _client = new();

    [Fact]
    public async Task BasicMutation_Success()
    {
        var mutation = new UseMutation<string, int>(new MutationOptions<string, int>
        {
            MutationFn = async id => { await Task.Delay(10); return $"item-{id}"; }
        }, _client);

        var result = await mutation.MutateAsync(42);

        Assert.Equal("item-42", result);
        Assert.Equal("item-42", mutation.Data);
        Assert.Equal(MutationStatus.Success, mutation.Status);
        Assert.True(mutation.IsSuccess);
        Assert.False(mutation.IsError);
        Assert.False(mutation.IsPending);
        Assert.False(mutation.IsIdle);
    }

    [Fact]
    public async Task BasicMutation_Error()
    {
        var mutation = new UseMutation<string, int>(new MutationOptions<string, int>
        {
            MutationFn = _ => throw new InvalidOperationException("fail")
        }, _client);

        await Assert.ThrowsAsync<InvalidOperationException>(() => mutation.MutateAsync(1));

        Assert.Equal(MutationStatus.Error, mutation.Status);
        Assert.True(mutation.IsError);
        Assert.NotNull(mutation.Error);
        Assert.Equal("fail", mutation.Error.Message);
    }

    [Fact]
    public async Task StatusTransitions_IdleToPendingToSuccess()
    {
        var statuses = new List<MutationStatus>();
        var tcs = new TaskCompletionSource<string>();

        var mutation = new UseMutation<string, int>(new MutationOptions<string, int>
        {
            MutationFn = _ => tcs.Task
        }, _client);

        statuses.Add(mutation.Status); // Idle
        mutation.OnChange += () => statuses.Add(mutation.Status);

        var task = mutation.MutateAsync(1);
        // After calling MutateAsync, status should be Pending
        Assert.Equal(MutationStatus.Pending, mutation.Status);

        tcs.SetResult("done");
        await task;

        Assert.Contains(MutationStatus.Idle, statuses);
        Assert.Contains(MutationStatus.Pending, statuses);
        Assert.Contains(MutationStatus.Success, statuses);
    }

    [Fact]
    public async Task StatusTransitions_IdleToPendingToError()
    {
        var statuses = new List<MutationStatus>();
        var tcs = new TaskCompletionSource<string>();

        var mutation = new UseMutation<string, int>(new MutationOptions<string, int>
        {
            MutationFn = _ => tcs.Task
        }, _client);

        statuses.Add(mutation.Status);
        mutation.OnChange += () => statuses.Add(mutation.Status);

        var task = mutation.MutateAsync(1);
        tcs.SetException(new Exception("boom"));

        await Assert.ThrowsAsync<Exception>(() => task);

        Assert.Contains(MutationStatus.Idle, statuses);
        Assert.Contains(MutationStatus.Pending, statuses);
        Assert.Contains(MutationStatus.Error, statuses);
    }

    [Fact]
    public async Task Variables_TrackedAcrossCalls()
    {
        var mutation = new UseMutation<string, string>(new MutationOptions<string, string>
        {
            MutationFn = async v => { await Task.Yield(); return v.ToUpper(); }
        }, _client);

        await mutation.MutateAsync("hello");
        Assert.Equal("hello", mutation.Variables);

        await mutation.MutateAsync("world");
        Assert.Equal("world", mutation.Variables);
    }

    [Fact]
    public async Task Reset_ClearsAllState()
    {
        var mutation = new UseMutation<string, int>(new MutationOptions<string, int>
        {
            MutationFn = async v => { await Task.Yield(); return $"item-{v}"; }
        }, _client);

        await mutation.MutateAsync(1);
        Assert.Equal(MutationStatus.Success, mutation.Status);

        mutation.Reset();

        Assert.Equal(MutationStatus.Idle, mutation.Status);
        Assert.True(mutation.IsIdle);
        Assert.Null(mutation.Data);
        Assert.Null(mutation.Error);
        Assert.Null(mutation.SubmittedAt);
        Assert.Equal(0, mutation.FailureCount);
    }

    [Fact]
    public async Task OnMutate_CalledBeforeMutationFn()
    {
        var callOrder = new List<string>();

        var mutation = new UseMutation<string, int>(new MutationOptions<string, int>
        {
            MutationFn = async v =>
            {
                callOrder.Add("mutationFn");
                await Task.Yield();
                return "result";
            },
            OnMutate = async (variables, ctx) =>
            {
                callOrder.Add("onMutate");
                return "mutateResult";
            }
        }, _client);

        await mutation.MutateAsync(1);

        Assert.Equal(new[] { "onMutate", "mutationFn" }, callOrder);
    }

    [Fact]
    public async Task OnSuccess_ReceivesDataVariablesAndOnMutateResult()
    {
        string? capturedData = null;
        int capturedVars = 0;
        object? capturedOnMutateResult = null;

        var mutation = new UseMutation<string, int>(new MutationOptions<string, int>
        {
            MutationFn = async v => { await Task.Yield(); return $"item-{v}"; },
            OnMutate = async (v, ctx) => { await Task.Yield(); return "context-data"; },
            OnSuccess = async (data, variables, onMutateResult, ctx) =>
            {
                capturedData = data;
                capturedVars = variables;
                capturedOnMutateResult = onMutateResult;
                await Task.Yield();
            }
        }, _client);

        await mutation.MutateAsync(42);

        Assert.Equal("item-42", capturedData);
        Assert.Equal(42, capturedVars);
        Assert.Equal("context-data", capturedOnMutateResult);
    }

    [Fact]
    public async Task OnError_ReceivesExceptionVariablesAndOnMutateResult()
    {
        Exception? capturedError = null;
        object? capturedOnMutateResult = null;

        var mutation = new UseMutation<string, int>(new MutationOptions<string, int>
        {
            MutationFn = _ => throw new InvalidOperationException("fail"),
            OnMutate = async (v, ctx) => { await Task.Yield(); return "pre-mutation"; },
            OnError = async (ex, variables, onMutateResult, ctx) =>
            {
                capturedError = ex;
                capturedOnMutateResult = onMutateResult;
                await Task.Yield();
            }
        }, _client);

        mutation.Mutate(1);
        await Task.Delay(100); // Let fire-and-forget complete

        Assert.NotNull(capturedError);
        Assert.Equal("fail", capturedError!.Message);
        Assert.Equal("pre-mutation", capturedOnMutateResult);
    }

    [Fact]
    public async Task OnSettled_CalledOnSuccess()
    {
        bool settledCalled = false;
        string? settledData = default;
        Exception? settledException = null;

        var mutation = new UseMutation<string, int>(new MutationOptions<string, int>
        {
            MutationFn = async v => { await Task.Yield(); return "ok"; },
            OnSettled = async (data, error, variables, onMutateResult, ctx) =>
            {
                settledCalled = true;
                settledData = data;
                settledException = error;
                await Task.Yield();
            }
        }, _client);

        await mutation.MutateAsync(1);

        Assert.True(settledCalled);
        Assert.Equal("ok", settledData);
        Assert.Null(settledException);
    }

    [Fact]
    public async Task OnSettled_CalledOnError()
    {
        bool settledCalled = false;
        Exception? settledException = null;

        var mutation = new UseMutation<string, int>(new MutationOptions<string, int>
        {
            MutationFn = _ => throw new Exception("boom"),
            OnSettled = async (data, error, variables, onMutateResult, ctx) =>
            {
                settledCalled = true;
                settledException = error;
                await Task.Yield();
            }
        }, _client);

        mutation.Mutate(1);
        await Task.Delay(100);

        Assert.True(settledCalled);
        Assert.NotNull(settledException);
        Assert.Equal("boom", settledException!.Message);
    }

    [Fact]
    public async Task PerCallCallbacks_FireForLastCallOnly()
    {
        var tcs1 = new TaskCompletionSource<string>();
        var tcs2 = new TaskCompletionSource<string>();

        var mutation = new UseMutation<string, int>(new MutationOptions<string, int>
        {
            MutationFn = v => v == 1 ? tcs1.Task : tcs2.Task
        }, _client);

        var perCallSuccessCount1 = 0;
        var perCallSuccessCount2 = 0;

        // First call
        var task1 = mutation.MutateAsync(1, new MutateOptions<string, int>
        {
            OnSuccess = async (data, variables, onMutateResult, ctx) =>
            {
                perCallSuccessCount1++;
                await Task.Yield();
            }
        });

        // Second call (supersedes first for per-call callbacks)
        var task2 = mutation.MutateAsync(2, new MutateOptions<string, int>
        {
            OnSuccess = async (data, variables, onMutateResult, ctx) =>
            {
                perCallSuccessCount2++;
                await Task.Yield();
            }
        });

        tcs1.SetResult("result1");
        tcs2.SetResult("result2");

        // First task completes but its per-call callback should NOT fire (not the last call)
        try { await task1; } catch { }
        await task2;

        Assert.Equal(0, perCallSuccessCount1); // Not the last call
        Assert.Equal(1, perCallSuccessCount2); // Last call
    }

    [Fact]
    public async Task OptionLevelCallbacks_FireForEachCall()
    {
        var tcs1 = new TaskCompletionSource<string>();
        var tcs2 = new TaskCompletionSource<string>();
        var optionCallbackCount = 0;

        var mutation = new UseMutation<string, int>(new MutationOptions<string, int>
        {
            MutationFn = v => v == 1 ? tcs1.Task : tcs2.Task,
            OnSuccess = async (data, variables, onMutateResult, ctx) =>
            {
                Interlocked.Increment(ref optionCallbackCount);
                await Task.Yield();
            }
        }, _client);

        var task1 = mutation.MutateAsync(1);
        var task2 = mutation.MutateAsync(2);

        tcs1.SetResult("result1");
        tcs2.SetResult("result2");

        try { await task1; } catch { }
        await task2;

        // Option-level callbacks fire for EACH call
        Assert.Equal(2, optionCallbackCount);
    }

    [Fact]
    public async Task CallbackExecutionOrder_OptionLevelFirst()
    {
        var order = new List<string>();

        var mutation = new UseMutation<string, int>(new MutationOptions<string, int>
        {
            MutationFn = async v => { await Task.Yield(); return "ok"; },
            OnSuccess = async (data, variables, onMutateResult, ctx) =>
            {
                order.Add("option-onSuccess");
                await Task.Yield();
            },
            OnSettled = async (data, error, variables, onMutateResult, ctx) =>
            {
                order.Add("option-onSettled");
                await Task.Yield();
            }
        }, _client);

        await mutation.MutateAsync(1, new MutateOptions<string, int>
        {
            OnSuccess = async (data, variables, onMutateResult, ctx) =>
            {
                order.Add("mutate-onSuccess");
                await Task.Yield();
            },
            OnSettled = async (data, error, variables, onMutateResult, ctx) =>
            {
                order.Add("mutate-onSettled");
                await Task.Yield();
            }
        });

        Assert.Equal(new[]
        {
            "option-onSuccess",
            "mutate-onSuccess",
            "option-onSettled",
            "mutate-onSettled"
        }, order);
    }

    [Fact]
    public async Task MutateAsync_ThrowsOnError()
    {
        var mutation = new UseMutation<string, int>(new MutationOptions<string, int>
        {
            MutationFn = _ => throw new InvalidOperationException("async-fail")
        }, _client);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => mutation.MutateAsync(1));
        Assert.Equal("async-fail", ex.Message);
    }

    [Fact]
    public async Task Mutate_FireAndForget_DoesNotThrow()
    {
        var errorCallbackCalled = false;

        var mutation = new UseMutation<string, int>(new MutationOptions<string, int>
        {
            MutationFn = _ => throw new Exception("should not propagate"),
            OnError = async (ex, v, omr, ctx) =>
            {
                errorCallbackCalled = true;
                await Task.Yield();
            }
        }, _client);

        // Should not throw
        mutation.Mutate(1);
        await Task.Delay(100);

        Assert.True(errorCallbackCalled);
        Assert.Equal(MutationStatus.Error, mutation.Status);
    }

    [Fact]
    public async Task Retry_ConfigurableCount()
    {
        int callCount = 0;

        var mutation = new UseMutation<string, int>(new MutationOptions<string, int>
        {
            MutationFn = async v =>
            {
                callCount++;
                if (callCount < 3) throw new Exception($"attempt-{callCount}");
                await Task.Yield();
                return "success";
            },
            Retry = 3,
            RetryDelay = TimeSpan.FromMilliseconds(10)
        }, _client);

        var result = await mutation.MutateAsync(1);

        Assert.Equal("success", result);
        Assert.Equal(3, callCount); // 1 initial + 2 retries before success on 3rd
    }

    [Fact]
    public async Task Retry_DefaultIsZero_NoRetries()
    {
        int callCount = 0;

        var mutation = new UseMutation<string, int>(new MutationOptions<string, int>
        {
            MutationFn = _ =>
            {
                callCount++;
                throw new Exception("fail");
            }
        }, _client);

        await Assert.ThrowsAsync<Exception>(() => mutation.MutateAsync(1));
        Assert.Equal(1, callCount); // No retries
    }

    [Fact]
    public async Task SubmittedAt_SetOnMutate()
    {
        var before = DateTime.UtcNow;

        var mutation = new UseMutation<string, int>(new MutationOptions<string, int>
        {
            MutationFn = async v => { await Task.Yield(); return "ok"; }
        }, _client);

        await mutation.MutateAsync(1);

        var after = DateTime.UtcNow;
        Assert.NotNull(mutation.SubmittedAt);
        Assert.InRange(mutation.SubmittedAt!.Value, before, after);
    }

    [Fact]
    public async Task FailureCount_TrackedDuringRetries()
    {
        int callCount = 0;
        var failureCounts = new List<int>();

        var mutation = new UseMutation<string, int>(new MutationOptions<string, int>
        {
            MutationFn = _ =>
            {
                callCount++;
                throw new Exception($"fail-{callCount}");
            },
            Retry = 2,
            RetryDelay = TimeSpan.FromMilliseconds(10)
        }, _client);

        mutation.OnChange += () => failureCounts.Add(mutation.FailureCount);

        await Assert.ThrowsAsync<Exception>(() => mutation.MutateAsync(1));

        Assert.Equal(3, mutation.FailureCount); // Initial + 2 retries
        Assert.NotNull(mutation.FailureReason);
        Assert.Equal("fail-3", mutation.FailureReason!.Message);
    }

    [Fact]
    public async Task NetworkMode_PausesWhenOffline()
    {
        var onlineManagerMock = new Mock<IOnlineManager>();
        onlineManagerMock.Setup(m => m.IsOnline).Returns(false);

        var client = new QueryClient(onlineManagerMock.Object);

        var mutation = new UseMutation<string, int>(new MutationOptions<string, int>
        {
            MutationFn = async v => { await Task.Yield(); return "ok"; },
            NetworkMode = NetworkMode.Online
        }, client);

        await Assert.ThrowsAsync<InvalidOperationException>(() => mutation.MutateAsync(1));
        Assert.True(mutation.IsPaused);
    }

    [Fact]
    public async Task NetworkMode_Always_ExecutesWhileOffline()
    {
        var onlineManagerMock = new Mock<IOnlineManager>();
        onlineManagerMock.Setup(m => m.IsOnline).Returns(false);

        var client = new QueryClient(onlineManagerMock.Object);

        var mutation = new UseMutation<string, int>(new MutationOptions<string, int>
        {
            MutationFn = async v => { await Task.Yield(); return "ok"; },
            NetworkMode = NetworkMode.Always
        }, client);

        var result = await mutation.MutateAsync(1);
        Assert.Equal("ok", result);
        Assert.False(mutation.IsPaused);
    }

    [Fact]
    public async Task Scope_MutationsRunSerially()
    {
        var order = new List<int>();
        var gate1 = new TaskCompletionSource();
        var gate2 = new TaskCompletionSource();

        var mutation = new UseMutation<string, int>(new MutationOptions<string, int>
        {
            MutationFn = async v =>
            {
                order.Add(v);
                if (v == 1) await gate1.Task;
                if (v == 2) await gate2.Task;
                order.Add(v * 10); // completion marker
                return $"done-{v}";
            },
            Scope = new MutationScope { Id = "test-scope" }
        }, _client);

        var task1 = mutation.MutateAsync(1);
        var task2 = mutation.MutateAsync(2);

        // Only first mutation should have started
        await Task.Delay(50);
        Assert.Equal(new[] { 1 }, order);

        gate1.SetResult();
        await task1;

        // Now second should start
        await Task.Delay(50);
        Assert.Contains(2, order);

        gate2.SetResult();
        await task2;

        Assert.Equal(new[] { 1, 10, 2, 20 }, order);
    }

    [Fact]
    public async Task MutationContext_ProvidesQueryClient()
    {
        QueryClient? capturedClient = null;

        var mutation = new UseMutation<string, int>(new MutationOptions<string, int>
        {
            MutationFn = async v => { await Task.Yield(); return "ok"; },
            OnMutate = async (v, ctx) =>
            {
                capturedClient = ctx.Client;
                return null;
            }
        }, _client);

        await mutation.MutateAsync(1);

        Assert.Same(_client, capturedClient);
    }

    [Fact]
    public async Task OnMutate_CanDoOptimisticUpdate()
    {
        // Simulate optimistic update pattern: onMutate sets cache, onError rolls back
        var key = new QueryKey("todos");
        _client.Set(key, new List<string> { "item1" });

        object? savedSnapshot = null;

        var mutation = new UseMutation<string, string>(new MutationOptions<string, string>
        {
            MutationFn = async v =>
            {
                await Task.Yield();
                return v;
            },
            OnMutate = async (variables, ctx) =>
            {
                // Snapshot current data
                var snapshot = ctx.Client.Get<List<string>>(key);
                savedSnapshot = snapshot?.ToList(); // clone

                // Optimistically update
                var current = ctx.Client.Get<List<string>>(key) ?? new List<string>();
                current.Add(variables);
                ctx.Client.Set(key, current);

                return savedSnapshot;
            }
        }, _client);

        await mutation.MutateAsync("item2");

        var cached = _client.Get<List<string>>(key);
        Assert.NotNull(cached);
        Assert.Contains("item2", cached!);
    }

}
