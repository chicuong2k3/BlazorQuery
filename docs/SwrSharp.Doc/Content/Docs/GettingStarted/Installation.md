---
title: "Installation"
description: "How to install and setup SwrSharp"
order: 2
category: "Getting Started"
---


# Installation

## Prerequisites

- .NET 7.0 or later
- Blazor Server or Blazor WebAssembly project

## NuGet Package

Install SwrSharp via NuGet:

```bash
dotnet add package SwrSharp
```

Or using Package Manager:

```powershell
Install-Package SwrSharp
```

## Setup

### 1. Register Services

Add SwrSharp services to your `Program.cs`:

```csharp
using SwrSharp.Core;

var builder = WebApplication.CreateBuilder(args);

// Add SwrSharp services
builder.Services.AddSwrSharp();

// Other services...
builder.Services.AddRazorComponents();

var app = builder.Build();
// ...
```

### 2. Configure Options (Optional)

You can customize SwrSharp behavior globally:

```csharp
builder.Services.AddSwrSharp(options =>
{
    // Default cache time for queries (stale time)
    options.DefaultStaleTime = TimeSpan.FromMinutes(5);
    
    // Default garbage collection time
    options.DefaultGarbageCollectionTime = TimeSpan.FromMinutes(15);
    
    // Enable detailed logging
    options.EnableLogging = true;
});
```

### 3. Inject QueryClient

In your Blazor components, inject the `QueryClient`:

```csharp
@inject QueryClient QueryClient
```

## Verify Installation

Create a simple test page to verify everything is working:

```csharp
@page "/test"
@inject QueryClient QueryClient

<h1>SwrSharp Test</h1>
<p>QueryClient is ready: @(QueryClient != null)</p>

@code {
    protected override void OnInitialized()
    {
        // Your code here
    }
}
```

## Next Steps

- Read [Query Basics](/docs/guides/query-basics)
- Learn [Query Keys](/docs/guides/query-keys)
- Explore [API Reference](/docs/api/query-client)
