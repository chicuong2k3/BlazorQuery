# SwrSharp Documentation Deployment Guide
This guide explains how to deploy the SwrSharp documentation to GitHub Pages.
## Prerequisites
- GitHub repository with SwrSharp code
- GitHub account with permissions to configure repository settings
## Deployment Options
### Option 1: Automatic Deployment (GitHub Actions) ✅ Recommended
#### Step 1: Enable GitHub Pages
1. Go to your GitHub repository
2. Navigate to **Settings** → **Pages**
3. Under "Build and deployment":
   - **Source**: Select "GitHub Actions"
4. Save the settings
#### Step 2: Push Changes
The documentation will automatically deploy when you push to `main` or `master` branch:
```bash
cd /home/chicuong/Desktop/code/SwrSharp
git add .
git commit -m "Add SwrSharp documentation"
git push origin main
```
#### Step 3: Monitor Deployment
1. Go to the **Actions** tab in your GitHub repository
2. Watch the "Deploy SwrSharp Docs to GitHub Pages" workflow
3. Once completed, your site will be live at: `https://[username].github.io/SwrSharp/`
### Option 2: Manual Deployment
#### Build Static Site Locally
```bash
cd /home/chicuong/Desktop/code/SwrSharp/docs/SwrSharp.Doc
dotnet run -c Release
```
This generates static files in: `bin/Release/net10.0/publish/wwwroot/`
#### Deploy to Any Static Host
Copy contents of `wwwroot/` to:
- **Netlify**: Drag and drop to Netlify
- **Vercel**: Connect your repository
- **Azure Static Web Apps**: Use Azure CLI
- **AWS S3**: Upload to S3 bucket
## GitHub Actions Workflow Details
The workflow (`.github/workflows/publish.yml`) performs these steps:
1. **Checkout code** from repository
2. **Setup .NET 8.0** environment
3. **Restore dependencies** with `dotnet restore`
4. **Build** the project in Release mode
5. **Run** BlazorStatic to generate static files
6. **Upload** generated files as artifact
7. **Deploy** to GitHub Pages
## Triggering Manual Deployment
You can manually trigger deployment without pushing code:
1. Go to **Actions** tab
2. Select "Deploy SwrSharp Docs to GitHub Pages"
3. Click **Run workflow**
4. Select branch and click **Run workflow**
## Custom Domain Setup
To use a custom domain (e.g., `docs.swrsharp.dev`):
1. Add a `CNAME` file to `wwwroot/`:
   ```
   docs.swrsharp.dev
   ```
2. Configure DNS records:
   - Type: `CNAME`
   - Name: `docs` (or `@` for root domain)
   - Value: `[username].github.io`
3. In GitHub Settings → Pages:
   - Enter custom domain
   - Check "Enforce HTTPS"
## Troubleshooting
### Build Fails
**Issue**: GitHub Actions workflow fails during build
**Solutions**:
- Check .NET version in workflow matches project
- Verify all dependencies are restored
- Check build logs in Actions tab
### Pages Not Updating
**Issue**: Changes pushed but site not updating
**Solutions**:
- Check workflow completed successfully
- Clear browser cache
- Wait 2-3 minutes for GitHub Pages propagation
- Verify correct branch is configured
### 404 Errors
**Issue**: Some pages return 404
**Solutions**:
- Ensure all markdown files have proper front matter
- Check file paths and naming conventions
- Verify BlazorStatic configuration in `Program.cs`
## Local Testing Before Deployment
Always test locally before deploying:
```bash
# Development mode
cd docs/SwrSharp.Doc
dotnet run
# Production mode (what will be deployed)
dotnet run -c Release
```
Open `http://localhost:5000` and verify:
- All pages load correctly
- Navigation works
- Search functionality
- No broken links
- Images display properly
## Deployment Checklist
Before deploying:
- [ ] All documentation files have front matter
- [ ] No broken internal links
- [ ] Images and assets are included
- [ ] Build succeeds locally in Release mode
- [ ] GitHub Actions workflow file is committed
- [ ] GitHub Pages is enabled in repository settings
- [ ] Custom domain (if any) is configured
## Post-Deployment Verification
After deployment:
1. Visit deployed site URL
2. Test all navigation links
3. Verify search works
4. Check responsive design on mobile
5. Test all documentation pages
6. Verify syntax highlighting in code blocks
## Updating Documentation
To update published documentation:
1. Edit markdown files in `Content/Docs/`
2. Test locally with `dotnet run`
3. Commit and push changes
4. GitHub Actions will automatically redeploy
## Performance Optimization
The static site is optimized for performance:
- ✅ No server-side rendering needed
- ✅ All pages pre-generated
- ✅ CDN-friendly static assets
- ✅ Fast page loads
- ✅ SEO-friendly HTML
## Support
For deployment issues:
- Check GitHub Actions logs
- Review BlazorStatic documentation
- Open issue in SwrSharp repository
---
**Last Updated**: February 8, 2026  
**Version**: 1.0.0
