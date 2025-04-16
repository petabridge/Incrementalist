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

# Run tests for affected projects those matching a glob
incrementalist -b dev -r --target-glob "src/*.Tests.csproj" -- test -c Release
```

### Running as a Local Tool

When using Incrementalist as a local tool, you need to use `dotnet tool run` with an additional `--` before the Incrementalist commands:

```shell
# Get list of affected projects
dotnet incrementalist -- -b dev -f ./affected-projects.txt

# Build affected projects
dotnet incrementalist -- -b dev -r -- build -c Release --nologo

# Run tests with coverage
dotnet incrementalist -- -b dev -r -- test -c Release --no-build --logger:trx --collect:"XPlat Code Coverage" --results-directory ./testresults

# Run in parallel mode
dotnet incrementalist -- -b dev -r --parallel -- build -c Release --nologo

# Save affected projects AND run commands
dotnet incrementalist -- -b dev -f ./affected-projects.txt -r -- build -c Release --nologo
```

> ![NOTE]
> Don't call `dotnet tool run incrementalist` - this runs into some very annoying parse issues: https://github.com/petabridge/Incrementalist/issues/378

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

# Only include test projects in the final list
incrementalist -b dev --target-glob "**/*.Tests.csproj" -f ./affected-test-projects.txt

# Exclude test projects from the final list
incrementalist -b dev --skip-glob "**/*.Tests.csproj" -f ./affected-non-test-projects.txt

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
                        
  -c, --config          Optional. Path to the configuration file. Defaults to 
                        .incrementalist/incrementalist.json in the current directory.

  --skip-glob           Optional. Glob pattern to exclude projects from the final
                        list. Applied after analyzing dependencies. Can be used
                        multiple times.
                        
  --target-glob         Optional. Glob pattern to include only matching projects in
                        the final list. Applied after analyzing dependencies. Can
                        be used multiple times.

  --create-config       Optional. Create a new configuration file with current 
                        options. See docs/config.md for details.

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

## 🌐 Filtering Projects with Glob Patterns

After Incrementalist determines the initial set of affected projects based on Git changes and project dependencies, you can further refine this list using glob patterns.

- **`--target-glob "<pattern>"`**: Only includes projects whose paths match the specified glob pattern(s). If multiple patterns are provided, a project matching *any* of them will be included. This filter is applied first.
- **`--skip-glob "<pattern>"`**: Excludes projects whose paths match the specified glob pattern(s) from the list remaining after any `--target-glob` filters have been applied. If multiple patterns are provided, a project matching *any* of them will be excluded.

Both options can be specified multiple times on the command line.

**Example:** Find all affected projects, but only run the build command on non-test projects within the `src` directory.

```shell
incrementalist -b dev --target-glob "src/**/*.csproj" --skip-glob "**/*.Tests.csproj" -r -- build -c Release --nologo
```

## 📚 Documentation

- 🔍 [How It Works](docs/how-it-works.md) - Technical details and architecture
- 🏗️ [Building from Source](docs/building.md) - Build instructions and development setup
- ⚙️ [Configuration Files](docs/config.md) - Using JSON configuration files

## 📜 License

Licensed under the Apache License, Version 2.0: http://www.apache.org/licenses/LICENSE-2.0

Copyright 2015-2025 [Petabridge](https://petabridge.com/)
