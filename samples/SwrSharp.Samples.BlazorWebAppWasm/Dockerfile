# Multi-stage Dockerfile for standalone Blazor WebAssembly (net9.0)
# Build stage: use .NET SDK to build and publish the Client project
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy only what is needed for restore/build to keep context small
COPY SwrSharp.Samples.BlazorWebAppWasm.Client/SwrSharp.Samples.BlazorWebAppWasm.Client.csproj ./SwrSharp.Samples.BlazorWebAppWasm.Client/
COPY ..\..\src ./src
# Copy the client project files
COPY SwrSharp.Samples.BlazorWebAppWasm.Client/. ./SwrSharp.Samples.BlazorWebAppWasm.Client/

WORKDIR /src/SwrSharp.Samples.BlazorWebAppWasm.Client
RUN dotnet restore "SwrSharp.Samples.BlazorWebAppWasm.Client.csproj"
RUN dotnet publish -c Release -o /app/publish

# Final stage: serve static files with nginx
FROM nginx:alpine AS final
# Copy a custom nginx config which enables SPA fallback (index.html)
COPY nginx.conf /etc/nginx/conf.d/default.conf
# Copy published wwwroot from the build stage
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]

