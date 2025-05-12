# 🔄 Incrementalist

<img src="https://raw.githubusercontent.com/petabridge/Incrementalist/refs/heads/dev/docs/incrementalist-logo-dark.svg" width="90" alt="Incrementalist Logo" />

Incrementalist is a .NET tool that leverages [libgit2sharp](https://github.com/libgit2/libgit2sharp/)
and [Roslyn](https://docs.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/) to compute incremental build steps for large
.NET solutions. It helps optimize your CI/CD pipeline by building and testing only the projects affected by your
changes.

## 🎯 When to Use Incrementalist

Incrementalist is particularly valuable for:

- 🏗️ **Large Solutions**: If your solution contains dozens or hundreds of projects, Incrementalist can significantly
  reduce build times by only building what's necessary.
- 📦 **Monorepos**: When managing multiple applications or services in a single repository, Incrementalist helps identify
  and build only the affected components.
- 🌐 **Microservice Architectures**: In repositories containing multiple microservices, build only the services impacted
  by your changes.
- 🔗 **Complex Dependencies**: When projects have intricate dependencies, Incrementalist automatically determines the impacted build graph.
- ⚡ **CI/CD Optimization**: Reduce CI/CD pipeline execution time by skipping unnecessary builds and tests.

## ⚙️ Requirements

- .NET 8.0 SDK or later
- Git installed and available in the system PATH

## 📥 Installation

Incrementalist is available in two forms:

1. [Incrementalist Library](https://www.nuget.org/packages/Incrementalist/) - a .NET 8 library for programmatic use
2. [Incrementalist.Cmd](https://www.nuget.org/packages/Incrementalist.Cmd/) - a `dotnet tool` for command-line use (
   recommended)

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

When installed globally, run commands directly using the `incrementalist` command with one of the available verbs:

```shell
# Get list of affected projects
incrementalist run --dry -b dev -f ./affected-projects.txt

# List affected folders
incrementalist list-affected-folders -b dev -f ./affected-folders.txt

# Run tests for affected projects
incrementalist run -b dev -- test -c Release

# Run tests for affected projects those matching a glob
incrementalist run -b dev --target-glob "src/*.Tests.csproj" -- test -c Release
```

### Running as a Local Tool

When using Incrementalist as a local tool, you need to use `dotnet tool run` with an additional `--` before the
Incrementalist commands:

```shell
# Get list of affected projects
dotnet incrementalist -- -b dev -f ./affected-projects.txt

# List affected folders
dotnet incrementalist -- list-affected-folders -b dev -f ./affected-folders.txt

# Build affected projects
dotnet incrementalist -- run -b dev -- build -c Release --nologo

# Run tests with coverage
dotnet incrementalist -- run -b dev -- test -c Release --no-build --logger:trx --collect:"XPlat Code Coverage" --results-directory ./testresults

# Run in parallel mode
dotnet incrementalist -- run -b dev --parallel -- build -c Release --nologo

# Save affected projects AND run commands
dotnet incrementalist -- -b dev -f ./affected-projects.txt run -- build -c Release --nologo
```

> ![NOTE]
> Don't call `dotnet tool run incrementalist` - this runs into some very annoying parse
> issues: https://github.com/petabridge/Incrementalist/issues/378 . Just call `dotnet incrementalist` instead.

## 📄 Configuration Files

Incrementalist supports JSON configuration files to store commonly used settings. This eliminates the need to specify
the same command-line arguments repeatedly.

```shell
# Use default configuration file (.incrementalist/incrementalist.json)
incrementalist run -- build

# Specify a custom configuration file
incrementalist -c my-config.json run -- build

# Create configuration file with current settings
incrementalist create-config -b dev --verbose --parallel
```

Create a configuration file in your repository:

```json
{
  "gitBranch": "dev",
  "verbose": true,
  "runInParallel": true
}
```

Command-line arguments take precedence over configuration file settings.
See [Configuration Documentation](docs/config.md) for complete details.

## 🚀 Quick Start Examples

```shell
# Get list of affected projects and save to file
incrementalist run --dry -b dev -f ./affected-projects.txt

# Specify solution explicitly
incrementalist run --dry -s ./src/MySolution.sln -b dev -f ./affected-projects.txt

# Get list of affected folders
incrementalist list-affected-folders -b dev -f ./affected-folders.txt

# Build only affected projects
incrementalist run -b dev -- build -c Release --nologo

# Run tests for affected projects
incrementalist run -b dev -- test -c Release --no-build --nologo

# Only include test projects in the final list
incrementalist run -b dev --target-glob "**/*.Tests.csproj" -f ./affected-test-projects.txt

# Exclude test projects from the final list
incrementalist run -b dev --skip-glob "**/*.Tests.csproj" -f ./affected-non-test-projects.txt

# Run tests with code coverage
incrementalist run -b dev -- test -c Release --no-build --nologo /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:CoverletOutput=./coverage/

# Save affected projects AND run commands
incrementalist run -b dev -f ./affected-projects.txt -- build -c Release --nologo

# Create configuration file with current settings (default path: .incrementalist/incrementalist.json)
incrementalist create-config -b dev --verbose --parallel

# Create configuration file with current settings and custom file name
incrementalist create-config -b dev --verbose --parallel -c ./my-incrementalist-config.json

# Run incrementalist with a custom configuration file
incrementalist run -c ./my-incrementalist-config.json -- build -c Release

# Perform a dry run without executing commands
incrementalist run -b dev --dry -- build -c Release --nologo
```

## 📄 Output Files

Incrementalist can generate two types of output files using `-f, --file`:

1. **Project Lists** (with the `run` verb):
   ```
   D:\src\Project1\Project1.csproj,D:\src\Project2\Project2.csproj
   ```

2. **Folder Lists** (with the `list-affected-folders` verb):
   ```
   D:\src\Project1,D:\src\Project2\SubFolder
   ```

These files can be used in build scripts, CI/CD pipelines, or other automation tools.

## 🛠️ Command-Line Options

Incrementalist now uses a verb-based command structure. Common options are available across all verbs, with some
verb-specific options.

### Common Options (available for all verbs)

```
  -s, --sln                   Optional. Solution file to analyze. Uses first .sln in
                              current directory if not specified.

  -f, --file                  Optional. Write output to the specified file.

  -b, --branch                Optional. Git branch to compare against
                              (e.g., 'dev' or 'master').

  -d, --dir                   Optional. Working directory. Defaults to current directory.

  --verbose                   Optional. (Default: false) Enable debug logging.

  -t, --timeout               Optional. (Default: 2) Solution load timeout in minutes.

  -c, --config                Optional. Path to the configuration file. Defaults to 
                              .incrementalist/incrementalist.json in the current directory.

  --continue-on-error         Optional. (Default: true) Continue executing commands even
                              if some fail.

  --parallel                  Optional. (Default: false) Execute commands in parallel.

  --fail-on-no-projects       Optional. (Default: false) Fail if no projects are affected.
                        
  --skip-glob                 Optional. Glob pattern to exclude projects from the final
                              list. Applied after analyzing dependencies. Can be used
                              multiple times.

  --target-glob               Optional. Glob pattern to include only matching projects in
                              the final list. Applied after analyzing dependencies. Can
                              be used multiple times.

  --help                      Display help screen.

  --version                   Display version information.
```

### Available Verbs

#### `create-config`

Creates a new configuration file with current options.

```
incrementalist create-config [options]
```

#### `list-affected-folders`

List affected folders instead of .NET projects.

```
incrementalist list-affected-folders [options]
```

#### `run`

Run a command against affected projects. Use the `--dry` option to test without executing.

```
incrementalist run [options] -- [dotnet command and arguments]
```

Additional options for `run`:

```
  --dry                 Optional. (Default: false) Performs a dry run without
                        executing any commands. Useful for testing.
```

#### `run-process`

Run a custom process against affected projects. Currently a discoverable alias for `run` that only supports dotnet commands.

```
incrementalist run-process [options] -- [process arguments]
```

Additional options for `run-process`:

```
  --process             Required. The name or path of the process to start (e.g. 'dotnet', '/bin/bash', 'mytool').
                        Currently only 'dotnet' is supported.

  --dry                 Optional. (Default: false) Performs a dry run without
                        executing any commands. Useful for testing.
```

## ⚡ Running Commands

Execute dotnet CLI commands against affected projects using the `run` verb:

```shell
# Build affected projects
incrementalist run -b dev -- build -c Release --nologo

# Run tests
incrementalist run -b dev -- test -c Release --no-build --nologo

# Run in parallel
incrementalist run -b dev --parallel -- build -c Release --nologo

# Stop on first error
incrementalist run -b dev --continue-on-error=false -- build -c Release --nologo

# Perform a dry run (shows commands without executing them)
incrementalist run -b dev --dry -- build -c Release --nologo
```

## 🌐 Filtering Projects with Glob Patterns

After Incrementalist determines the initial set of affected projects based on Git changes and project dependencies, you
can further refine this list using glob patterns.

- **`--target-glob "<pattern>"`**: Only includes projects whose paths match the specified glob pattern(s). If multiple
  patterns are provided, a project matching *any* of them will be included. This filter is applied first.
- **`--skip-glob "<pattern>"`**: Excludes projects whose paths match the specified glob pattern(s) from the list
  remaining after any `--target-glob` filters have been applied. If multiple patterns are provided, a project matching
  *any* of them will be excluded.

Both options can be specified multiple times on the command line.

**Example:** Find all affected projects, but only run the build command on non-test projects within the `src` directory.

```shell
incrementalist run -b dev --target-glob "src/**/*.csproj" --skip-glob "**/*.Tests.csproj" -- build -c Release --nologo
```

## 📚 Documentation

- 🔍 [How It Works](docs/how-it-works.md) - Technical details and architecture
- 🏗️ [Building from Source](docs/building.md) - Build instructions and development setup
- ⚙️ [Configuration Files](docs/config.md) - Using JSON configuration files
- 🌍 [Real-World Examples](docs/examples.md) - Production examples from other open source projects using Incrementalist in the wild.

## 📜 License

Licensed under the Apache License, Version 2.0: http://www.apache.org/licenses/LICENSE-2.0

Copyright 2015-2025 [Petabridge](https://petabridge.com/)
