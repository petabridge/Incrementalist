# Building Incrementalist

This guide covers building Incrementalist from source and contributing to its development.

## Prerequisites

- .NET 8.0 SDK or later
- Git installed and available in the system PATH
- PowerShell (Windows) or Bash (Linux/macOS)

## Build Commands

### Basic Build

**Windows**
```powershell
./build.ps1 all
```

**Linux / macOS**
```bash
./build.sh all
```

For help on available commands:
```bash
build.[cmd|sh] help
```

## Build Targets

The build system is powered by [FAKE](https://fake.build/) and includes the following targets:

- `Clean` - Cleans build artifacts
- `RestorePackages` - Restores NuGet packages
- `Build` - Compiles the solution
- `RunTests` - Executes unit tests
- `IntegrationTests` - Runs integration tests
- `NBench` - Runs performance tests
- `CreateNuget` - Creates NuGet packages
- `SignPackages` - Signs NuGet packages (if configured)
- `PublishNuget` - Publishes packages to NuGet
- `DocFx` - Generates documentation

## Project Conventions

The build system follows these conventions:

- Projects ending in `.Tests` are treated as [XUnit2](https://xunit.github.io/) tests
- Projects ending in `.Tests.Performance` are treated as [NBench](https://github.com/petabridge/NBench) tests
- Other projects are treated as NuGet packaging targets

## Documentation

### API Documentation
- Uses [DocFx](http://dotnet.github.io/docfx/)
- Articles go in `/docs/articles/`
- API docs are auto-generated
- Output is placed in `/docs/_site/`

### Preview Documentation
```powershell
serve-docs.cmd
```

## Release Process

### Version Management
- Release notes are maintained in `RELEASE_NOTES.md`
- Version numbers are managed in `Directory.Build.props`
- Package versions are centrally managed in `Directory.Packages.props`

### Code Signing

Incrementalist uses [SignService](https://github.com/onovotny/SignService) for code-signing NuGet packages.

1. Set up SignService server
2. Configure settings:
   - Add AD settings to `appsettings.json`
   - Set signing info in `build.fsx`

3. Sign packages:
```powershell
build.cmd nuget SignClientSecret={secret} SignClientUser={username}
```

## Verification Steps

After making changes:

1. Run basic verification:
```bash
dotnet build
dotnet test
```

2. Run full verification:

**Windows**
```powershell
./build.ps1 RunTests
./build.ps1 IntegrationTests
```

**Linux/macOS**
```bash
./build.sh RunTests
./build.sh IntegrationTests
```

3. If relevant, run performance tests:
```bash
./build.[cmd|sh] NBench
``` 