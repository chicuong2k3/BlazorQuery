using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SwrSharp.Blazor;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddSwrSharp();

await builder.Build().RunAsync();
