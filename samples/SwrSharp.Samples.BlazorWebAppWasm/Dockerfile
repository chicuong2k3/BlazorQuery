# Multi-stage Dockerfile for standalone Blazor WebAssembly (net8.0)
# Build stage: use .NET SDK to build and publish the Client project
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /workspace

# Copy only the necessary top-level folders while preserving relative layout
# so ProjectReference paths inside the client project resolve correctly.
COPY samples/ samples/
COPY src/ src/

# Restore & publish the Client project (paths are preserved under /workspace)
RUN dotnet restore "samples/SwrSharp.Samples.BlazorWebAppWasm/SwrSharp.Samples.BlazorWebAppWasm.Client/SwrSharp.Samples.BlazorWebAppWasm.Client.csproj"
RUN dotnet publish "samples/SwrSharp.Samples.BlazorWebAppWasm/SwrSharp.Samples.BlazorWebAppWasm.Client/SwrSharp.Samples.BlazorWebAppWasm.Client.csproj" -c Release -o /app/publish

# Final stage: serve static files with nginx
FROM nginx:alpine AS final
# Copy a custom nginx config which enables SPA fallback (index.html)
COPY samples/SwrSharp.Samples.BlazorWebAppWasm/nginx.conf /etc/nginx/conf.d/default.conf
# Copy published wwwroot from the build stage
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
