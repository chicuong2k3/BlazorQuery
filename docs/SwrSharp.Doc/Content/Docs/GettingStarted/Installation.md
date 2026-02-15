---
title: "Installation"
description: "How to install and setup SwrSharp"
order: 2
category: "Getting Started"
---


# Installation

## Prerequisites

- .NET 8.0 or later
- Blazor Server or Blazor WebAssembly project

## NuGet Packages

Install the core library and Blazor integration:

```bash
dotnet add package SwrSharp.Core
dotnet add package SwrSharp.Blazor
```

Or using Package Manager:

```powershell
Install-Package SwrSharp.Core
Install-Package SwrSharp.Blazor
```

## Setup

### 1. Register Services

Add SwrSharp services to your `Program.cs`:

```csharp
using SwrSharp.Blazor;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Add SwrSharp services (registers QueryClient, BrowserFocusManager, BrowserOnlineManager)
builder.Services.AddSwrSharp();

await builder.Build().RunAsync();
```

### 2. Add QueryClientProvider

Wrap your app with `QueryClientProvider` in your `Routes.razor` or layout:

```razor
<QueryClientProvider>
    <Router AppAssembly="typeof(Program).Assembly">
        <Found Context="routeData">
            <RouteView RouteData="routeData" DefaultLayout="typeof(Layout.MainLayout)"/>
            <FocusOnNavigate RouteData="routeData" Selector="h1"/>
        </Found>
    </Router>
</QueryClientProvider>
```

### 3. Configure Options (Optional)

You can customize SwrSharp behavior globally:

```csharp
builder.Services.AddSwrSharp(options =>
{
    options.DefaultNetworkMode = NetworkMode.Online;
    options.DefaultRefetchOnWindowFocus = true;
});
```

### 4. Add Imports

Add these to your `_Imports.razor`:

```razor
@using SwrSharp.Core
@using SwrSharp.Blazor
```

## Verify Installation

Create a simple component that uses `UseQuery`:

```razor
@page "/test"
@inherits SwrSharpComponentBase

@if (greeting.IsLoading)
{
    <p>Loading...</p>
}
else
{
    <p>@greeting.Data</p>
}

@code {
    private UseQuery<string> greeting = null!;

    protected override void OnParametersSet()
    {
        greeting = UseQuery(new QueryOptions<string>(
            queryKey: new QueryKey("greeting"),
            queryFn: async _ => "Hello from SwrSharp!"
        ));
    }
}
```

## Next Steps

- Read the [Blazor Integration](/docs/guides/blazor-integration) guide
- Learn about [Query Keys](/docs/guides/query-keys)
- Explore [API Reference](/docs/api/use-query)
