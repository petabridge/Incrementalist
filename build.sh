#!/usr/bin/env bash
# Define default arguments
SCRIPT_NAME=$(basename "$0")
TARGET="Build"
CONFIGURATION="Release"
VERBOSITY="verbose"
DRYRUN=
NOTEST=0
NOINTEGRATIONTEST=0
NOPACK=0
NOSIGN=0

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case "$1" in
        -t|--target)
            TARGET="$2"
            shift 2
            ;;
        -c|--configuration)
            CONFIGURATION="$2"
            shift 2
            ;;
        --notest)
            NOTEST=1
            shift
            ;;
        --nointegrationtest)
            NOINTEGRATIONTEST=1
            shift
            ;;
        --nopack)
            NOPACK=1
            shift
            ;;
        --nosign)
            NOSIGN=1
            shift
            ;;
        *)
            echo "Invalid argument: $1"
            exit 1
            ;;
    esac
done

# Clean
echo "Cleaning..."
dotnet clean -c "$CONFIGURATION"
rm -rf ./bin
rm -rf ./TestResults
rm -rf ./PerfResults

# Restore tools
echo "Restoring .NET tools..."
dotnet tool restore

# Build
echo "Building..."
dotnet build -c "$CONFIGURATION"

# Tests
if [ $NOTEST -eq 0 ]; then
    echo "Running tests..."
    dotnet test -c "$CONFIGURATION" --no-build --logger:trx --logger:"console;verbosity=normal" --results-directory ./TestResults
fi

# Integration Tests
if [ $NOINTEGRATIONTEST -eq 0 ]; then
    echo "Running integration tests..."
    frameworks=("net6.0" "net7.0" "net8.0")
    for framework in "${frameworks[@]}"; do
        echo "Testing framework $framework..."
        # Folders-only check
        dotnet run --project ./src/Incrementalist.Cmd/Incrementalist.Cmd.csproj -c "$CONFIGURATION" --framework "$framework" --no-build -- -b dev -l -f ./TestResults/incrementalist-affected-folders.txt
        # Solution check
        dotnet run --project ./src/Incrementalist.Cmd/Incrementalist.Cmd.csproj -c "$CONFIGURATION" --framework "$framework" --no-build -- -b dev -f ./TestResults/incrementalist-affected-files.txt
    done
fi

# Pack
if [ $NOPACK -eq 0 ]; then
    echo "Creating NuGet packages..."
    find ./src -name "*.csproj" -not -name "*Tests*" -exec dotnet pack {} -c "$CONFIGURATION" --no-build --include-symbols -o ./bin/nuget \;
fi

echo "Build completed successfully!"