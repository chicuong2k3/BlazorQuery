#!/bin/bash
echo "======================================"
echo "Testing Static Site Generation"
echo "======================================"
echo ""
cd "$(dirname "$0")"
echo "1. Cleaning previous builds..."
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
echo "5. Running static site generator..."
cd publish
ASPNETCORE_ENVIRONMENT=Production dotnet SwrSharp.Doc.dll &
PID=$!
echo "   Waiting for site generation (10 seconds)..."
sleep 10
kill $PID 2>/dev/null
cd ..
echo ""
echo "6. Checking output..."
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
    else
        echo "❌ No files generated!"
        exit 1
    fi
else
    echo "❌ Output directory not found!"
    exit 1
fi
echo ""
echo "======================================"
