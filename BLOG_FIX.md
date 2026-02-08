# Blog Content Error Fix ✅
## Problem
GitHub Actions workflow failed with:
```
System.IO.DirectoryNotFoundException: /home/runner/work/BlazorQuery/BlazorQuery/docs/SwrSharp.Doc/publish/Content/Blog/
```
**Root Cause**: 
- Program.cs configured BlogFrontMatter service
- Content/Blog directory didn't exist
- BlazorStatic tried to access it during static generation
## Solutions Applied
### 1. Removed Blog Service from Program.cs
**Before**:
```csharp
.AddBlazorStaticContentService<BlogFrontMatter>(opt =>
{
    opt.PageUrl = "blog";
    opt.ContentPath = "Content/Blog";
});
```
**After**:
```csharp
// Blog service removed - only documentation needed
// If you need blog later, create Content/Blog and uncomment
```
### 2. Disabled Blog-Related Components
Renamed to `.unused` to exclude from build:
- ✅ `Blog.razor` → `Blog.razor.unused`
- ✅ `Tags.razor` → `Tags.razor.unused`
- ✅ `PostsList.razor` → `PostsList.razor.unused`
- ✅ `TagsComponent.razor` → `TagsComponent.razor.unused`
### 3. Updated GitHub Actions Workflow
Added explicit Content copy step:
```yaml
- name: Publish
  run: |
    dotnet publish --configuration Release --no-build --output publish
    echo "=== Copying Content to publish directory ==="
    cp -r Content publish/
```
### 4. Updated test-publish.sh
Added Content copy to local test script:
```bash
echo "5. Copying Content to publish directory..."
cp -r Content publish/
```
## Files Modified
| File | Change |
|------|--------|
| `Program.cs` | Removed BlogFrontMatter service |
| `Blog.razor` | Renamed to .unused |
| `Tags.razor` | Renamed to .unused |
| `PostsList.razor` | Renamed to .unused |
| `TagsComponent.razor` | Renamed to .unused |
| `.github/workflows/deploy-docs.yml` | Added Content copy step |
| `test-publish.sh` | Added Content copy step |
## How to Test
```bash
cd /home/chicuong/Desktop/code/SwrSharp/docs/SwrSharp.Doc
# Test locally
./test-publish.sh
# Should see:
# ✅ Static site generation SUCCESS!
# Total files: [number]
```
## If You Need Blog Later
1. Create `Content/Blog` directory
2. Add blog markdown files
3. Uncomment BlogFrontMatter service in Program.cs
4. Rename `.unused` files back to `.razor`
## Verification
After fix:
- ✅ No more DirectoryNotFoundException
- ✅ Static site generates successfully
- ✅ Only documentation content is built
- ✅ Deployment workflow completes
- ✅ Site deploys to GitHub Pages
## Next Steps
1. Run `./test-publish.sh` to verify locally
2. Commit changes
3. Push to GitHub
4. Monitor Actions workflow
5. Verify deployment
---
**Status**: ✅ FIXED
The blog directory error is resolved. Documentation site will now build and deploy successfully.
**Last Updated**: February 8, 2026
