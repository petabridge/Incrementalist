# 🔄 Incrementalist

<img src="https://raw.githubusercontent.com/petabridge/Incrementalist/refs/heads/dev/docs/incrementalist-logo-dark.svg" width="90" alt="Incrementalist Logo" />

Incrementalist is a .NET tool that leverages [libgit2sharp](https://github.com/libgit2/libgit2sharp/) and [Roslyn](https://docs.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/) to compute incremental build steps for large .NET solutions. It helps optimize your CI/CD pipeline by building and testing only the projects affected by your changes.

## 🎯 When to Use Incrementalist

Incrementalist is particularly valuable for:

- 🏗️ **Large Solutions**: If your solution contains dozens or hundreds of projects, Incrementalist can significantly reduce build times by only building what's necessary.
- 📦 **Monorepos**: When managing multiple applications or services in a single repository, Incrementalist helps identify and build only the affected components.
- 🌐 **Microservice Architectures**: In repositories containing multiple microservices, build only the services impacted by your changes.
- 🔗 **Complex Dependencies**: When projects have intricate dependencies, Incrementalist automatically determines the complete build graph.
- ⚡ **CI/CD Optimization**: Reduce CI/CD pipeline execution time by skipping unnecessary builds and tests.

## ⚙️ Requirements

- .NET 8.0 SDK or later
- Git installed and available in the system PATH

## 📥 Installation

Incrementalist is available in two forms:

1. [Incrementalist Library](https://www.nuget.org/packages/Incrementalist/) - a .NET 8 library for programmatic use
2. [Incrementalist.Cmd](https://www.nuget.org/packages/Incrementalist.Cmd/) - a `dotnet tool` for command-line use (recommended)

Install the command-line tool globally:

```shell
dotnet tool install --global Incrementalist.Cmd
```

Or install locally in your project:

```shell
# From your repository root
dotnet new tool-manifest # if you haven't already created a .config/dotnet-tools.json
dotnet tool install Incrementalist.Cmd
```

### Running as a Global Tool

When installed globally, run commands directly using the `incrementalist` command:

```shell
# Get list of affected projects
incrementalist -b dev -f ./affected-projects.txt

# Run tests for affected projects
incrementalist -b dev -r -- test -c Release --no-build --nologo
```

### Running as a Local Tool

When using Incrementalist as a local tool, you need to use `dotnet tool run` with an additional `--` before the Incrementalist commands:

```shell
# Get list of affected projects
dotnet tool run incrementalist -- -b dev -f ./affected-projects.txt

# Build affected projects
dotnet tool run incrementalist -- -b dev -r -- build -c Release --nologo

# Run tests with coverage
dotnet tool run incrementalist -- -b dev -r -- test -c Release --no-build --logger:trx --collect:"XPlat Code Coverage" --results-directory ./testresults

# Run in parallel mode
dotnet tool run incrementalist -- -b dev -r --parallel -- build -c Release --nologo

# Save affected projects AND run commands
dotnet tool run incrementalist -- -b dev -f ./affected-projects.txt -r -- build -c Release --nologo
```

Note the command structure when using as a local tool:
- First `--` after `dotnet tool run incrementalist` is for Incrementalist options
- Second `--` (if using `-r`) is for the dotnet command to run on affected projects

## 📄 Configuration Files

Incrementalist supports JSON configuration files to store commonly used settings. This eliminates the need to specify the same command-line arguments repeatedly.

```shell
# Use default configuration file (.incrementalist/incrementalist.json)
incrementalist -r -- build

# Specify a custom configuration file
incrementalist -c my-config.json -r -- build
```

Create a configuration file in your repository:

```json
{
  "gitBranch": "master",
  "solutionFilePath": "src/MySolution.sln",
  "verbose": true,
  "runInParallel": true
}
```

Command-line arguments take precedence over configuration file settings. See [Configuration Documentation](docs/config.md) for complete details.

## 🚀 Quick Start Examples

```shell
# Get list of affected projects and save to file
incrementalist -b dev -f ./affected-projects.txt

# Specify solution explicitly
incrementalist -s ./src/MySolution.sln -b dev -f ./affected-projects.txt

# Get list of affected folders
incrementalist -b dev -l -f ./affected-folders.txt

# Build only affected projects
incrementalist -b dev -r -- build -c Release --nologo

# Run tests for affected projects
incrementalist -b dev -r -- test -c Release --no-build --nologo

# Run tests with code coverage
incrementalist -b dev -r -- test -c Release --no-build --nologo /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:CoverletOutput=./coverage/

# Save affected projects AND run commands
incrementalist -b dev -f ./affected-projects.txt -r -- build -c Release --nologo

# Create configuration file with current settings (default path: .incrementalist/incrementalist.json)
incrementalist -b dev --verbose --parallel --create-config

# Create configuration file with current settings and custom file name
incrementalist -b dev --verbose --parallel --create-config -c ./my-incrementalist-config.json

# Run incrementalist with a custom configuration file
incrementalist -c ./my-incrementalist-config.json -r -- build -c Release
```

## 📄 Output Files

Incrementalist can generate two types of output files using `-f, --file`:

1. **Project Lists** (default):
   ```
   D:\src\Project1\Project1.csproj,D:\src\Project2\Project2.csproj
   ```

2. **Folder Lists** (with `-l, --folders-only`):
   ```
   D:\src\Project1,D:\src\Project2\SubFolder
   ```

These files can be used in build scripts, CI/CD pipelines, or other automation tools.

## 🛠️ Command-Line Options

```
  -s, --sln             Optional. Solution file to analyze. Uses first .sln in
                        current directory if not specified.

  -f, --file            Optional. Write output to the specified file.

  -l, --folders-only    Optional. List affected folders instead of projects.

  -b, --branch          Optional. (Default: dev) Git branch to compare against
                        (e.g., 'dev' or 'master').

  -d, --dir             Optional. Working directory. Defaults to current directory.

  --verbose             Optional. (Default: false) Enable debug logging.

  -t, --timeout         Optional. (Default: 2) Solution load timeout in minutes.

  -r, --run             Optional. Run dotnet CLI command against affected projects.
                        All arguments after -- are passed to dotnet.

  --continue-on-error   Optional. (Default: true) Continue executing commands even
                        if some fail.

  --parallel            Optional. (Default: false) Execute commands in parallel.

  --fail-on-no-projects Optional. (Default: false) Fail if no projects are affected.

  --no-cache            Optional. (Default: false) Ignore any existing cache file
                        and perform a full Roslyn analysis.
                        
  -c, --config          Optional. Path to the configuration file. Defaults to 
                        .incrementalist/incrementalist.json in the current directory.

  --help                Display help screen.

  --version             Display version information.
```

## ⚡ Running Commands

Execute dotnet CLI commands against affected projects:

```shell
# Build affected projects
incrementalist -b dev -r -- build -c Release --nologo

# Run tests
incrementalist -b dev -r -- test -c Release --no-build --nologo

# Run in parallel
incrementalist -b dev -r --parallel -- build -c Release --nologo

# Stop on first error
incrementalist -b dev -r --continue-on-error=false -- build -c Release --nologo
```

## 📚 Documentation

- 🔍 [How It Works](docs/how-it-works.md) - Technical details and architecture
- 🏗️ [Building from Source](docs/building.md) - Build instructions and development setup
- ⚡ [Dependency Graph Caching](docs/caching.md) - Cache system explanation and best practices
- ⚙️ [Configuration Files](docs/config.md) - Using JSON configuration files

## 📜 License

Licensed under the Apache License, Version 2.0: http://www.apache.org/licenses/LICENSE-2.0

Copyright 2015-2025 [Petabridge](https://petabridge.com/)
