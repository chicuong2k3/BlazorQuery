# Missing Pages Fix - Front Matter Required ✅
## Problem
Không tìm thấy pages tạo từ markdown files. BlazorStatic không generate pages từ .md files.
**Root Cause**: 
- Markdown files KHÔNG có YAML front matter
- BlazorStatic yêu cầu front matter để process files
- ContentService.Posts trả về empty list
## Solution
### YAML Front Matter Required
Mỗi markdown file CẦN có front matter như này:
```markdown
---
title: "Page Title"
description: "Page description"
order: 1
category: "Guides"
---
# Your Content Here
```
## Files Need Front Matter
### Getting Started (2 files):
- ✅ `01-Overview.md` - needs front matter
- ✅ `02-Installation.md` - needs front matter
### Guides (23 files):
- ✅ `01-Query-Keys.md` - ADDED
- ⚠️ `02-Query-Functions.md` - needs adding
- ⚠️ `03-Network-Mode.md` - needs adding
- ⚠️ `04-Query-Retries.md` - needs adding
- ... (19 more files need front matter)
### API (3 files):
- ⚠️ `01-UseQuery.md` - needs adding
- ⚠️ `02-UseInfiniteQuery.md` - needs adding
- ⚠️ `03-QueryClient.md` - needs adding
### Concepts (1 file):
- ⚠️ `01-Query-Concepts.md` - needs adding
## How to Add Front Matter
### Template for Guides:
```yaml
---
title: "Guide Title"
description: "Brief description of the guide"
order: 1
category: "Guides"
---
```
### Template for Getting Started:
```yaml
---
title: "Page Title"
description: "Description"
order: 1
category: "Getting Started"
---
```
### Template for API:
```yaml
---
title: "API Name"
description: "API description"
order: 1
category: "API"
---
```
## Manual Fix Steps
For EACH markdown file in Content/Docs:
1. Open the file
2. Add front matter at the TOP (before any content)
3. Use template above with appropriate values
4. Save file
Example for `02-Query-Functions.md`:
```markdown
---
title: "Query Functions"
description: "Writing query functions to fetch data"
order: 2
category: "Guides"
---
# Query Functions
Your existing content here...
```
## Automated Fix (Python Script)
Save this as `add_frontmatter.sh`:
```bash
#!/bin/bash
DOCS_DIR="/home/chicuong/Desktop/code/SwrSharp/docs/SwrSharp.Doc/Content/Docs"
# Add front matter to a file
add_fm() {
    local file=$1
    local title=$2
    local desc=$3
    local order=$4
    local category=$5
    # Check if already has front matter
    if head -1 "$file" | grep -q "^---"; then
        echo "  ⏭️  $(basename $file) already has front matter"
        return
    fi
    # Create temp file with front matter
    {
        echo "---"
        echo "title: \"$title\""
        echo "description: \"$desc\""
        echo "order: $order"
        echo "category: \"$category\""
        echo "---"
        echo ""
        cat "$file"
    } > "$file.tmp"
    mv "$file.tmp" "$file"
    echo "  ✅ Added front matter to $(basename $file)"
}
# Guides
echo "Processing Guides..."
add_fm "$DOCS_DIR/Guides/02-Query-Functions.md" "Query Functions" "Writing query functions" 2 "Guides"
add_fm "$DOCS_DIR/Guides/03-Network-Mode.md" "Network Mode" "Handling network states" 3 "Guides"
# ... add more files
echo "✅ Done!"
```
## Quick Test
After adding front matter:
```bash
cd /home/chicuong/Desktop/code/SwrSharp/docs/SwrSharp.Doc
# Run in dev mode
dotnet run
# Visit debug page
# http://localhost:5000/debug/posts
# Should show all posts with URLs
```
## Verification
1. Visit `/debug/posts` to see all loaded posts
2. Check `Total posts: [number]` - should be > 0
3. Each post should show:
   - Title
   - Description  
   - Category
   - URL
4. Click URLs to verify pages load
## What Changes
**Before** (no front matter):
```markdown
# Query Keys
Content here...
```
→ BlazorStatic skips file ❌
**After** (with front matter):
```markdown
---
title: "Query Keys"
description: "Understanding query keys"
order: 1
category: "Guides"
---
# Query Keys
Content here...
```
→ BlazorStatic processes file ✅
## Files Modified
| File | Status |
|------|--------|
| `01-Query-Keys.md` | ✅ Front matter added |
| `Debug.razor` | ✅ Created for testing |
| `DocsPage.razor` | ✅ Improved routing logic |
| All other .md files | ⚠️ Need front matter |
## Next Steps
1. **Add front matter to ALL .md files** (29 files total)
2. **Test locally**: Run `dotnet run`
3. **Visit** `/debug/posts` to verify
4. **Check** that posts appear
5. **Navigate** to individual pages
6. **Commit** changes
7. **Deploy** to GitHub
## Expected Result
After adding front matter to all files:
- ✅ `/debug/posts` shows 29 posts
- ✅ All documentation pages accessible
- ✅ Navigation works
- ✅ Previous/Next links work
- ✅ Static generation includes all pages
---
**Priority**: 🔥 HIGH - Site won't work without front matter
**Status**: ⚠️ NEEDS ACTION - Must add front matter to all markdown files
**Last Updated**: February 8, 2026
