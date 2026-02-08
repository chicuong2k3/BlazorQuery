#!/bin/bash
echo "======================================"
echo "Testing Static Site Generation"
echo "======================================"
echo ""
cd "$(dirname "$0")"
echo "1. Cleaning previous builds..."
rm -rf publish output bin/Release/net10.0/output
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
echo "6. Running static site generator..."
cd publish
ASPNETCORE_ENVIRONMENT=Production timeout 180s dotnet SwrSharp.Doc.dll || true
cd ..
echo ""
echo "7. Checking output..."
# Check multiple possible locations
OUTPUT_DIR=""
if [ -d "output" ]; then
    OUTPUT_DIR="output"
    echo "✓ Output directory found at: ./output"
elif [ -d "publish/output" ]; then
    OUTPUT_DIR="publish/output"
    echo "✓ Output directory found at: ./publish/output"
elif [ -d "bin/Release/net10.0/output" ]; then
    OUTPUT_DIR="bin/Release/net10.0/output"
    echo "✓ Output directory found at: ./bin/Release/net10.0/output"
else
    echo "❌ Output directory not found!"
    echo ""
    echo "Searching for output directory..."
    find . -name "output" -type d 2>/dev/null || echo "No output directory found anywhere"
    echo ""
    echo "This could mean:"
    echo "  1. BlazorStatic generator didn't run"
    echo "  2. Generator crashed without creating output"
    echo "  3. Output path is configured differently"
    echo ""
    echo "Check the logs above for errors."
    exit 1
fi
echo ""
echo "Files generated in ${OUTPUT_DIR}:"
find "${OUTPUT_DIR}" -type f | head -20
echo ""
FILE_COUNT=$(find "${OUTPUT_DIR}" -type f | wc -l)
echo "Total files: $FILE_COUNT"
if [ $FILE_COUNT -gt 0 ]; then
    echo ""
    echo "✅ Static site generation SUCCESS!"
    echo ""
    echo "Output location: $(pwd)/${OUTPUT_DIR}"
    echo ""
    echo "To preview locally, run:"
    echo "  cd ${OUTPUT_DIR} && python3 -m http.server 8000"
    echo "  Then open: http://localhost:8000"
else
    echo "❌ No files generated!"
    exit 1
fi
echo ""
echo "======================================"
