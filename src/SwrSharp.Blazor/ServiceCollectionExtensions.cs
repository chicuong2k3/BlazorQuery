using Microsoft.Extensions.DependencyInjection;
using SwrSharp.Core;

namespace SwrSharp.Blazor;

/// <summary>
/// Extension methods for registering SwrSharp services in the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers SwrSharp services including QueryClient, BrowserFocusManager, and BrowserOnlineManager.
    /// </summary>
    public static IServiceCollection AddSwrSharp(
        this IServiceCollection services,
        Action<SwrSharpOptions>? configure = null)
    {
        var options = new SwrSharpOptions();
        configure?.Invoke(options);

        services.AddScoped<BrowserFocusManager>();
        services.AddScoped<BrowserOnlineManager>();

        services.AddScoped<QueryClient>(sp =>
        {
            var focusManager = sp.GetRequiredService<BrowserFocusManager>();
            var onlineManager = sp.GetRequiredService<BrowserOnlineManager>();
            var client = new QueryClient(onlineManager: onlineManager, focusManager: focusManager);
            client.DefaultNetworkMode = options.DefaultNetworkMode;
            client.DefaultRefetchOnWindowFocus = options.DefaultRefetchOnWindowFocus;
            return client;
        });

        return services;
    }
}
