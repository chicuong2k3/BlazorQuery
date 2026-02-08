# Quick Deploy Guide
## Test Locally First
```bash
cd /home/chicuong/Desktop/code/SwrSharp/docs/SwrSharp.Doc
# Test static site generation
./test-publish.sh
# If successful, you'll see output directory with generated files
```
## Deploy to GitHub Pages
### Step 1: Commit Changes
```bash
cd /home/chicuong/Desktop/code/SwrSharp
git add .
git commit -m "Update SwrSharp documentation with React Query design"
git push origin main
```
### Step 2: Enable GitHub Pages
1. Go to repository **Settings**
2. Click **Pages** in sidebar
3. Under "Build and deployment":
   - **Source**: Select "GitHub Actions"
4. Save
### Step 3: Monitor Deployment
1. Go to **Actions** tab
2. Watch "Deploy SwrSharp Docs to GitHub Pages"
3. Wait for completion (3-5 minutes)
4. Visit: `https://[username].github.io/SwrSharp/`
## Troubleshooting
### If workflow fails at "Generate static site":
Check the debug output in Actions log. The workflow will show:
- Directory structure
- Whether output directory was created
- Number of files generated
### Common Issues:
**Issue**: Output directory not found
**Solution**: Check Program.cs - ensure `UseBlazorStaticGenerator` is called in Production
**Issue**: Build succeeds but no files
**Solution**: Run `./test-publish.sh` locally to debug
**Issue**: Workflow times out
**Solution**: Increase sleep time in workflow or add timeout parameter
## Verify Deployment
After deployment:
- [ ] Homepage loads
- [ ] Sidebar navigation works
- [ ] All documentation pages accessible
- [ ] Code syntax highlighting works
- [ ] Previous/Next navigation works
- [ ] Search box displays
## Quick Commands
```bash
# Test locally
dotnet run
# Test static generation
./test-publish.sh
# Build for production
./build.sh prod
# Clean
./build.sh clean
```
---
**Need help?** Check DEPLOYMENT.md for detailed guide.
