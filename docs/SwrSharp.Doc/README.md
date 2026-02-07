# SwrSharp Documentation

Official documentation for SwrSharp - a powerful data fetching and caching library for Blazor applications, inspired by TanStack Query.

## 🚀 Features

- **Complete Documentation**: Comprehensive guides covering all SwrSharp features
- **TanStack Query v5 Design**: Modern dark theme matching TanStack Query aesthetics
- **Static Site Generation**: Powered by [BlazorStatic](https://github.com/tesar-tech/BlazorStatic)
- **29 Documentation Pages**: Including getting started, guides, concepts, and API reference
- **GitHub Actions Deployment**: Automatic publishing to GitHub Pages

## 📚 Documentation Structure

- **Getting Started** (2 pages): Overview and Installation
- **Guides** (23 pages): Comprehensive feature guides
- **Concepts** (1 page): Core concepts explanation
- **API Reference** (3 pages): UseQuery, UseInfiniteQuery, QueryClient

## 🛠️ Local Development

```bash
cd docs/SwrSharp.Doc
dotnet run
```

Then open: `http://localhost:5000`

## 📦 Building for Production

```bash
dotnet run -c Release
```

Static files will be generated in `bin/Release/net10.0/wwwroot/`

## 🌐 Deployment

### GitHub Pages (Automatic)

1. **Enable GitHub Pages** in repository settings
2. **Configure GitHub Actions** workflow (see `.github/workflows/publish.yml`)
3. **Push changes** - GitHub Actions will automatically build and deploy

### Manual Deployment

1. Build the site: `dotnet run -c Release`
2. Deploy contents of `bin/Release/net10.0/wwwroot/` to your hosting provider

## 🎨 Design

The documentation design is inspired by [TanStack Query v5](https://tanstack.com/query/latest) with:
- Dark theme (`#0d1117` background)
- Red accent colors
- Responsive sidebar navigation
- Search functionality
- Modern typography

## 📝 Adding Documentation

1. Create markdown files in `Content/Docs/` subdirectories
2. Add YAML front matter:
   ```yaml
   ---
   title: "Page Title"
   description: "Page description"
   order: 1
   category: "Guides"
   ---
   ```
3. Content will be automatically indexed and displayed

## 🔧 Technology Stack

- **Framework**: Blazor (ASP.NET Core)
- **Static Generator**: BlazorStatic 1.0.0-beta.17
- **Styling**: Tailwind CSS
- **Markdown**: Built-in support with syntax highlighting

## 📄 License

MIT License - See LICENSE file for details

## 🙏 Acknowledgements

- Design inspired by [TanStack Query](https://tanstack.com/query/latest)
- Powered by [BlazorStatic](https://github.com/tesar-tech/BlazorStatic)
- Built with [Blazor](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor)

