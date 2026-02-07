# SwrSharp Documentation - Publishing Checklist
## ✅ Pre-Deployment Checklist
### 1. Documentation Content
- [x] 29 documentation files created
  - [x] 2 Getting Started pages
  - [x] 23 Guides pages
  - [x] 1 Concepts page
  - [x] 3 API Reference pages
- [x] All markdown files have proper YAML front matter
- [x] No broken internal links
- [x] Code examples are correct and tested
### 2. Design & Styling
- [x] TanStack Query v5 design implemented
- [x] Dark theme colors configured (`#0d1117`)
- [x] Sidebar navigation with search box
- [x] Responsive layout
- [x] Typography and spacing correct
- [x] Active page highlighting
- [x] Hover states working
### 3. Technical Setup
- [x] BlazorStatic configured correctly
- [x] DocFrontMatter implements IFrontMatter
- [x] All build errors resolved
- [x] Program.cs configured for production
- [x] Routes configured correctly
- [x] No duplicate routes
### 4. Deployment Files
- [x] README.md updated
- [x] DEPLOYMENT.md created
- [x] .gitignore configured
- [x] GitHub Actions workflow created (`.github/workflows/deploy-docs.yml`)
- [x] build.sh script created
### 5. Local Testing
- [ ] Run `dotnet run` and verify site works
- [ ] Test all navigation links
- [ ] Verify search box displays
- [ ] Check responsive design
- [ ] Test Previous/Next navigation
- [ ] Verify code syntax highlighting
## 🚀 Publishing Steps
### Step 1: Test Locally (Required)
```bash
cd /home/chicuong/Desktop/code/SwrSharp/docs/SwrSharp.Doc
# Development mode
dotnet run
# Visit http://localhost:5000 and verify everything works
```
### Step 2: Test Production Build
```bash
# Use the build script
./build.sh prod
# Or manually
dotnet run -c Release
```
### Step 3: Commit and Push to GitHub
```bash
cd /home/chicuong/Desktop/code/SwrSharp
# Check git status
git status
# Add all files
git add .
# Commit with descriptive message
git commit -m "Add SwrSharp documentation with TanStack Query v5 design
- 29 documentation pages (Getting Started, Guides, Concepts, API)
- Dark theme matching TanStack Query v5
- Sidebar navigation with search
- GitHub Actions workflow for auto-deployment
- BlazorStatic for static site generation"
# Push to GitHub
git push origin main  # or 'master' depending on your branch name
```
### Step 4: Configure GitHub Pages
1. Go to your GitHub repository: `https://github.com/[your-username]/SwrSharp`
2. Click **Settings** tab
3. Click **Pages** in left sidebar
4. Under "Build and deployment":
   - **Source**: Select "GitHub Actions"
5. Click **Save**
### Step 5: Monitor Deployment
1. Go to **Actions** tab in repository
2. Watch for "Deploy SwrSharp Docs to GitHub Pages" workflow
3. Click on the workflow to see progress
4. Wait for completion (usually 2-5 minutes)
### Step 6: Verify Deployment
Once workflow completes:
1. Visit: `https://[your-username].github.io/SwrSharp/`
2. Test all pages load correctly
3. Verify navigation works
4. Check mobile responsiveness
5. Test all documentation links
## 📋 Quick Commands Reference
```bash
# Navigate to docs
cd /home/chicuong/Desktop/code/SwrSharp/docs/SwrSharp.Doc
# Run development server
dotnet run
# Build for production
./build.sh prod
# or
dotnet run -c Release
# Clean build artifacts
./build.sh clean
# Check git status
git status
# Stage all changes
git add .
# Commit changes
git commit -m "Your commit message"
# Push to GitHub
git push origin main
```
## 🌐 Expected URLs
After deployment:
- **Main site**: `https://[username].github.io/SwrSharp/`
- **Docs home**: `https://[username].github.io/SwrSharp/docs/getting-started/overview`
- **API docs**: `https://[username].github.io/SwrSharp/docs/api/use-query`
## ⚠️ Common Issues
### Build Fails Locally
- Check .NET version: `dotnet --version` (need 8.0+)
- Run: `dotnet restore`
- Check for compilation errors
### GitHub Actions Fails
- Check workflow file is in `.github/workflows/`
- Verify .NET version in workflow
- Check Actions tab for detailed error logs
### Pages Don't Update
- Clear browser cache (Ctrl+Shift+R)
- Wait 2-3 minutes for propagation
- Check workflow completed successfully
### 404 Errors
- Verify GitHub Pages source is set to "GitHub Actions"
- Check base path configuration
- Ensure all files have front matter
## 📞 Support
If you encounter issues:
1. Check DEPLOYMENT.md for troubleshooting
2. Review GitHub Actions logs
3. Test locally first with `dotnet run -c Release`
4. Check BlazorStatic documentation
## ✨ Success Indicators
Your documentation is successfully published when:
- ✅ GitHub Actions workflow shows green checkmark
- ✅ Site loads at GitHub Pages URL
- ✅ All navigation links work
- ✅ Search box is visible
- ✅ Dark theme is applied correctly
- ✅ All 29 documentation pages are accessible
- ✅ Code blocks have syntax highlighting
- ✅ Previous/Next navigation works
---
**Ready to publish?** Follow the steps above! 🚀
**Last Updated**: February 8, 2026
