#!/bin/bash
echo "======================================"
echo "SwrSharp Documentation Build Script"
echo "======================================"
echo ""
# Colors
GREEN='\033[0;32m'
BLUE='\033[0;34m'
RED='\033[0;31m'
NC='\033[0m' # No Color
# Functions
print_success() {
    echo -e "${GREEN}✓ $1${NC}"
}
print_info() {
    echo -e "${BLUE}ℹ $1${NC}"
}
print_error() {
    echo -e "${RED}✗ $1${NC}"
}
# Check if we're in the right directory
if [ ! -f "SwrSharp.Doc.csproj" ]; then
    print_error "Please run this script from the SwrSharp.Doc directory"
    exit 1
fi
# Parse command line arguments
MODE=${1:-dev}
if [ "$MODE" = "dev" ] || [ "$MODE" = "development" ]; then
    print_info "Building in DEVELOPMENT mode..."
    echo ""
    print_info "Cleaning previous build..."
    dotnet clean > /dev/null 2>&1
    print_success "Cleaned"
    print_info "Restoring dependencies..."
    dotnet restore
    if [ $? -eq 0 ]; then
        print_success "Dependencies restored"
    else
        print_error "Failed to restore dependencies"
        exit 1
    fi
    print_info "Building project..."
    dotnet build
    if [ $? -eq 0 ]; then
        print_success "Build successful"
    else
        print_error "Build failed"
        exit 1
    fi
    echo ""
    print_success "Ready to run in development mode!"
    echo ""
    print_info "Start the server with: dotnet run"
    print_info "Open: http://localhost:5000"
elif [ "$MODE" = "prod" ] || [ "$MODE" = "production" ]; then
    print_info "Building for PRODUCTION..."
    echo ""
    print_info "Cleaning previous build..."
    dotnet clean > /dev/null 2>&1
    print_success "Cleaned"
    print_info "Restoring dependencies..."
    dotnet restore
    if [ $? -eq 0 ]; then
        print_success "Dependencies restored"
    else
        print_error "Failed to restore dependencies"
        exit 1
    fi
    print_info "Building in Release mode..."
    dotnet build --configuration Release
    if [ $? -eq 0 ]; then
        print_success "Build successful"
    else
        print_error "Build failed"
        exit 1
    fi
    print_info "Generating static site..."
    dotnet run --configuration Release --no-build
    if [ $? -eq 0 ]; then
        print_success "Static site generated"
    else
        print_error "Static site generation failed"
        exit 1
    fi
    echo ""
    print_success "Production build complete!"
    echo ""
    print_info "Static files are in: bin/Release/net10.0/publish/wwwroot/"
    print_info "Deploy these files to your hosting provider"
elif [ "$MODE" = "clean" ]; then
    print_info "Cleaning all build artifacts..."
    dotnet clean
    rm -rf bin obj
    print_success "Cleaned successfully"
else
    print_error "Unknown mode: $MODE"
    echo ""
    echo "Usage: ./build.sh [mode]"
    echo ""
    echo "Modes:"
    echo "  dev, development  - Build for development (default)"
    echo "  prod, production  - Build and generate static site for production"
    echo "  clean            - Clean all build artifacts"
    exit 1
fi
echo ""
echo "======================================"
