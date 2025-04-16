# Building Incrementalist

This guide covers building Incrementalist from source and contributing to its development.

## Prerequisites

- .NET 8.0 SDK or later
- Git installed and available in the system PATH
- PowerShell (Windows) or Bash (Linux/macOS)

## Basic Build Commands

### Building the Solution

```bash
# Build with default configuration (Debug)
dotnet build

# Build with Release configuration
dotnet build -c Release
```

### Running Unit Tests

```bash
# Run all unit tests
dotnet test

# Run tests with Release configuration
dotnet test -c Release

# Run tests with detailed output
dotnet test --logger:trx --logger:"console;verbosity=normal"
```

### Creating NuGet Packages

```bash
# Create packages (after building)
dotnet pack -c Release -o bin/nuget

# Create packages with symbols
dotnet pack -c Release -o bin/nuget --include-symbols
```

## Integration Tests

The integration test suite validates core Incrementalist functionality through a series of end-to-end tests.

### Running Integration Tests

```powershell
./scripts/integration-tests.ps1
```

You can specify the build configuration:
```powershell
./scripts/integration-tests.ps1 -Configuration Release
```

### Test Cases

The integration suite includes the following tests:

1. **List affected folders**
   - Tests the folder-level change detection using the `list-affected-folders` verb
   - Verifies correct output to `incrementalist-affected-folders.txt`

2. **Default project list**
   - Tests solution-wide change analysis
   - Validates affected files detection
   - Outputs results to `incrementalist-affected-files.txt`

3. **Command execution**
   - Validates command execution on affected projects using the `run` verb
   - Tests basic command routing and execution

4. **Parallel execution**
   - Tests parallel command execution across projects
   - Verifies concurrent operation behavior

5. **Error handling**
   - Verifies proper error handling behavior
   - Tests failure scenarios and exit codes

6. **Configuration generation**
   - Tests configuration file creation using the `create-config` verb
   - Verifies settings are properly saved

### Test Output

The integration test suite provides detailed output:
- Clear [PASS]/[FAIL] status for each test
- Summary of total tests run
- Number of passed tests (including expected failures)
- Number of failed tests
- Exit code 0 if all tests behave as expected
- Exit code 1 if any test has unexpected behavior

## Version Management

Versions and release notes are managed through:
- `RELEASE_NOTES.md` - Contains version history and release notes
- `Directory.Build.props` - Contains current version and package metadata
- `Directory.Packages.props` - Contains package version dependencies

### Updating Versions

The version is automatically updated from `RELEASE_NOTES.md` when building. The process:
1. Reads the latest version from `RELEASE_NOTES.md`
2. Updates `Directory.Build.props` with the version and release notes
3. Applies to all subsequent build operations

## Publishing Packages

### Creating Release Packages

```bash
# Build and create packages
dotnet build -c Release
dotnet pack -c Release -o bin/nuget --include-symbols --no-build
```

### Publishing to NuGet

```bash
# Push package to NuGet (replace with your API key)
dotnet nuget push bin/nuget/*.nupkg --api-key YOUR_API_KEY --source https://api.nuget.org/v3/index.json
```

## Verification Process

When making changes, follow these steps:

1. **Basic Verification**
```bash
# Build and test
dotnet build
dotnet test
```

2. **Full Verification**
```bash
# Build Release
dotnet build -c Release

# Run all tests
dotnet test -c Release --no-build

# Run integration tests
./scripts/integration-tests.ps1 -Configuration Release
```

3. **Package Verification** (for releases)
```bash
# Create packages
dotnet pack -c Release -o bin/nuget --include-symbols

# Verify package contents
# Check bin/nuget/*.nupkg contents using NuGet Package Explorer or similar tool
``` 