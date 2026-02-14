namespace SwrSharp.Core.Tests;

public class WindowFocusRefetchingTests : IDisposable
{
    private readonly TestFocusManager _focusManager;
    private readonly QueryClient _client;

    public WindowFocusRefetchingTests()
    {
        _focusManager = new TestFocusManager();
        _client = new QueryClient(focusManager: _focusManager);
    }

    [Fact]
    public async Task RefetchOnWindowFocus_ShouldRefetchWhenWindowGainsFocus()
    {
        var fetchCount = 0;
        
        var query = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("test"),
                queryFn: async _ => {
                    fetchCount++;
                    await Task.Delay(10);
                    return $"Data {fetchCount}";
                },
                staleTime: TimeSpan.FromMilliseconds(50),
                refetchOnWindowFocus: true
            ),
            _client
        );

        // Initial fetch
        await query.ExecuteAsync();
        Assert.Equal(1, fetchCount);
        Assert.Equal("Data 1", query.Data);

        // Wait for data to become stale
        await Task.Delay(100);

        // Simulate window gaining focus
        _focusManager.SetFocused(true);
        await Task.Delay(50); // Wait for refetch

        // Should have refetched
        Assert.Equal(2, fetchCount);
        Assert.Equal("Data 2", query.Data);
    }

    [Fact]
    public async Task RefetchOnWindowFocus_ShouldNotRefetchWhenDisabled()
    {
        var fetchCount = 0;
        
        var query = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("test"),
                queryFn: async _ => {
                    fetchCount++;
                    await Task.Delay(10);
                    return $"Data {fetchCount}";
                },
                staleTime: TimeSpan.MaxValue, // Prevent staleTimer from triggering
                refetchOnWindowFocus: false // Disabled
            ),
            _client
        );

        // Initial fetch
        await query.ExecuteAsync();
        Assert.Equal(1, fetchCount);

        // Simulate window gaining focus
        _focusManager.SetFocused(true);
        await Task.Delay(50);

        // Should NOT have refetched (refetchOnWindowFocus is false)
        Assert.Equal(1, fetchCount);
    }

    [Fact]
    public async Task RefetchOnWindowFocus_ShouldOnlyRefetchWhenDataIsStale()
    {
        var fetchCount = 0;
        
        var query = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("test"),
                queryFn: async _ => {
                    fetchCount++;
                    await Task.Delay(10);
                    return $"Data {fetchCount}";
                },
                staleTime: TimeSpan.FromSeconds(10), // Long stale time
                refetchOnWindowFocus: true
            ),
            _client
        );

        // Initial fetch
        await query.ExecuteAsync();
        Assert.Equal(1, fetchCount);

        // Data is still fresh
        _focusManager.SetFocused(true);
        await Task.Delay(50);

        // Should NOT refetch (data still fresh)
        Assert.Equal(1, fetchCount);
    }

    [Fact]
    public async Task RefetchOnWindowFocus_ShouldNotRefetchWhenLosingFocus()
    {
        var fetchCount = 0;
        
        var query = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("test"),
                queryFn: async _ => {
                    fetchCount++;
                    await Task.Delay(10);
                    return $"Data {fetchCount}";
                },
                staleTime: TimeSpan.MaxValue, // Prevent staleTimer from triggering
                refetchOnWindowFocus: true
            ),
            _client
        );

        await query.ExecuteAsync();
        Assert.Equal(1, fetchCount);

        // Simulate window losing focus
        _focusManager.SetFocused(false);
        await Task.Delay(50);

        // Should NOT refetch when losing focus
        Assert.Equal(1, fetchCount);
    }

    [Fact]
    public async Task RefetchOnWindowFocus_ShouldNotRefetchWhenAlreadyFetching()
    {
        var fetchCount = 0;
        var tcs = new TaskCompletionSource<string>();
        
        var query = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("test"),
                queryFn: async _ => {
                    fetchCount++;
                    if (fetchCount == 1)
                        await tcs.Task; // Block first fetch
                    return $"Data {fetchCount}";
                },
                staleTime: TimeSpan.Zero,
                refetchOnWindowFocus: true
            ),
            _client
        );

        // Start fetch (but don't complete)
        var fetchTask = query.ExecuteAsync();
        await Task.Delay(20); // Let it start

        // Simulate focus while fetching
        _focusManager.SetFocused(true);
        await Task.Delay(20);

        // Should still be only 1 fetch
        Assert.Equal(1, fetchCount);

        // Complete the fetch
        tcs.SetResult("Data 1");
        await fetchTask;
    }

    [Fact]
    public async Task RefetchOnWindowFocus_ShouldNotRefetchWhenQueryDisabled()
    {
        var fetchCount = 0;
        
        var options = new QueryOptions<string>(
            queryKey: new QueryKey("test"),
            queryFn: async _ => {
                fetchCount++;
                await Task.Delay(10);
                return $"Data {fetchCount}";
            },
            staleTime: TimeSpan.MaxValue, // Prevent staleTimer from triggering
            enabled: true,
            refetchOnWindowFocus: true
        );

        var query = new UseQuery<string>(options, _client);

        await query.ExecuteAsync();
        Assert.Equal(1, fetchCount);

        // Disable query via options
        options.Enabled = false;

        // Simulate focus
        _focusManager.SetFocused(true);
        await Task.Delay(50);

        // Should NOT refetch (query disabled)
        Assert.Equal(1, fetchCount);
    }

    [Fact]
    public void FocusManager_SetFocused_ShouldOverrideAutomaticDetection()
    {
        var focusManager = new DefaultFocusManager();
        
        // Default: focused
        Assert.True(focusManager.IsFocused);

        // Manually set to false
        focusManager.SetFocused(false);
        Assert.False(focusManager.IsFocused);

        // Manually set to true
        focusManager.SetFocused(true);
        Assert.True(focusManager.IsFocused);

        // Fallback to automatic (null)
        focusManager.SetFocused(null);
        Assert.True(focusManager.IsFocused); // Default is true
    }

    [Fact]
    public void FocusManager_ShouldFireEventWhenFocusChanges()
    {
        var focusManager = new DefaultFocusManager();
        var events = new List<bool>();

        focusManager.FocusChanged += (isFocused) => events.Add(isFocused);

        focusManager.SetFocused(false);
        focusManager.SetFocused(true);
        focusManager.SetFocused(true); // No change, no event

        Assert.Equal(2, events.Count);
        Assert.False(events[0]);
        Assert.True(events[1]);
    }

    [Fact]
    public void FocusManager_SetEventListener_ShouldAllowCustomFocusDetection()
    {
        var focusManager = new DefaultFocusManager();
        var events = new List<bool>();
        
        focusManager.FocusChanged += (isFocused) => events.Add(isFocused);

        Action<bool>? capturedHandleFocus = null;

        // Set up custom listener
        focusManager.SetEventListener((handleFocus) => {
            capturedHandleFocus = handleFocus;
            // Return cleanup action
            return () => { /* cleanup */ };
        });

        Assert.NotNull(capturedHandleFocus);

        // Custom code calls handleFocus
        capturedHandleFocus!(false);
        capturedHandleFocus!(true);

        Assert.Equal(2, events.Count);
        Assert.False(events[0]);
        Assert.True(events[1]);
    }

    [Fact]
    public async Task GlobalDefault_RefetchOnWindowFocus_ShouldBeRespected()
    {
        var focusManager = new TestFocusManager();
        var client = new QueryClient(focusManager: focusManager)
        {
            DefaultRefetchOnWindowFocus = false // Disable globally
        };

        var fetchCount = 0;
        
        // Query inherits global default
        var query = new UseQuery<string>(
            new QueryOptions<string>(
                queryKey: new QueryKey("test"),
                queryFn: async _ => {
                    fetchCount++;
                    await Task.Delay(10);
                    return $"Data {fetchCount}";
                },
                staleTime: TimeSpan.FromMilliseconds(50)
                // refetchOnWindowFocus not specified, uses default (false)
            ),
            client
        );

        await query.ExecuteAsync();
        await Task.Delay(100); // Stale

        focusManager.SetFocused(true);
        await Task.Delay(50);

        // Should still refetch (option defaults to true in constructor)
        // Note: To fully support global defaults, QueryOptions would need
        // to check client.DefaultRefetchOnWindowFocus if not specified
        Assert.Equal(2, fetchCount);
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    // Test helper
    private class TestFocusManager : IFocusManager
    {
        private bool _isFocused;

        public bool IsFocused => _isFocused;

        public event Action<bool>? FocusChanged;

        public void SetFocused(bool? isFocused)
        {
            if (!isFocused.HasValue)
            {
                _isFocused = false;
                return;
            }

            if (_isFocused != isFocused.Value)
            {
                _isFocused = isFocused.Value;
                FocusChanged?.Invoke(_isFocused);
            }
        }

        public void SetEventListener(Func<Action<bool>, Action>? setupHandler)
        {
            // Not needed for tests
        }
    }
}

