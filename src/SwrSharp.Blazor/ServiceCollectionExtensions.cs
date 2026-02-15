using Microsoft.Extensions.DependencyInjection;
using SwrSharp.Core;

namespace SwrSharp.Blazor;

/// <summary>
/// Extension methods for registering SwrSharp services in the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers a scoped QueryClient with optional configuration.
    /// </summary>
    public static IServiceCollection AddSwrSharp(
        this IServiceCollection services,
        Action<SwrSharpOptions>? configure = null)
    {
        var options = new SwrSharpOptions();
        configure?.Invoke(options);

        services.AddScoped<QueryClient>(_ =>
        {
            var client = new QueryClient();
            client.DefaultNetworkMode = options.DefaultNetworkMode;
            client.DefaultRefetchOnWindowFocus = options.DefaultRefetchOnWindowFocus;
            return client;
        });

        return services;
    }
}
