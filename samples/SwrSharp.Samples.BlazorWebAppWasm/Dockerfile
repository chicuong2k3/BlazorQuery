# Multi-stage Dockerfile for standalone Blazor WebAssembly (net9.0)
# Build stage: use .NET SDK to build and publish the Client project
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy only project files needed for restore to keep layer caching efficient
COPY samples/SwrSharp.Samples.BlazorWebAppWasm/SwrSharp.Samples.BlazorWebAppWasm.Client/SwrSharp.Samples.BlazorWebAppWasm.Client.csproj \
    SwrSharp.Samples.BlazorWebAppWasm.Client/
COPY src/SwrSharp.Core/SwrSharp.Core.csproj src/SwrSharp.Core/
COPY src/SwrSharp.Blazor/SwrSharp.Blazor.csproj src/SwrSharp.Blazor/

# Restore dependencies
RUN dotnet restore SwrSharp.Samples.BlazorWebAppWasm.Client/SwrSharp.Samples.BlazorWebAppWasm.Client.csproj

# Copy the rest of the source files
COPY samples/SwrSharp.Samples.BlazorWebAppWasm/SwrSharp.Samples.BlazorWebAppWasm.Client/ SwrSharp.Samples.BlazorWebAppWasm.Client/
COPY src/SwrSharp.Core/ src/SwrSharp.Core/
COPY src/SwrSharp.Blazor/ src/SwrSharp.Blazor/

WORKDIR /src/SwrSharp.Samples.BlazorWebAppWasm.Client
RUN dotnet publish -c Release -o /app/publish

# Final stage: serve static files with nginx
FROM nginx:alpine AS final
# Copy a custom nginx config which enables SPA fallback (index.html)
COPY samples/SwrSharp.Samples.BlazorWebAppWasm/nginx.conf /etc/nginx/conf.d/default.conf
# Copy published wwwroot from the build stage
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
