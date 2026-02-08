#!/bin/bash
echo "======================================"
echo "Testing Static Site Generation"
echo "======================================"
echo ""
cd "$(dirname "$0")"
echo "1. Cleaning previous builds..."
rm -rf publish output
dotnet clean > /dev/null 2>&1
echo "2. Restoring dependencies..."
dotnet restore
echo "3. Building in Release mode..."
dotnet build --configuration Release --no-restore
if [ $? -ne 0 ]; then
    echo "❌ Build failed!"
    exit 1
fi
echo "4. Publishing..."
dotnet publish --configuration Release --no-build --output publish
echo "5. Copying Content to publish directory..."
cp -r Content publish/
echo "   Content directory copied"
ls -la publish/Content/ | head -10
echo "6. Running static site generator..."
cd publish
ASPNETCORE_ENVIRONMENT=Production timeout 120s dotnet SwrSharp.Doc.dll &
PID=$!
echo "   Waiting for site generation (up to 120 seconds)..."
wait $PID
RESULT=$?
cd ..
echo ""
echo "7. Checking output..."
if [ -d "output" ]; then
    echo "✓ Output directory exists"
    echo ""
    echo "Files generated:"
    find output -type f | head -20
    echo ""
    FILE_COUNT=$(find output -type f | wc -l)
    echo "Total files: $FILE_COUNT"
    if [ $FILE_COUNT -gt 0 ]; then
        echo ""
        echo "✅ Static site generation SUCCESS!"
        echo ""
        echo "Output location: $(pwd)/output"
        echo ""
        echo "To preview locally, run:"
        echo "  cd output && python3 -m http.server 8000"
    else
        echo "❌ No files generated!"
        exit 1
    fi
else
    echo "❌ Output directory not found!"
    echo "Generator may have failed. Check logs above."
    exit 1
fi
echo ""
echo "======================================"
