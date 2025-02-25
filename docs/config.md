# Incrementalist Configuration Files

Incrementalist now supports configuration files to store commonly used settings. This eliminates the need to specify the same command-line arguments repeatedly.

## Configuration File Format

Incrementalist uses a JSON-based configuration file format. By default, Incrementalist looks for a file named `incrementalist.json` in the current directory, but you can specify a different file using the `-c` or `--config` command-line option.

## Available Settings

The following settings can be specified in the configuration file:

| Setting | Type | Description | CLI Equivalent |
|---------|------|-------------|----------------|
| `gitBranch` | string | The branch to compare against (e.g. "dev", "master") | `-b`, `--branch` |
| `solutionFilePath` | string | Path to the solution file to analyze | `-s`, `--sln` |
| `outputFile` | string | Path where affected projects will be written | `-f`, `--file` |
| `listFolders` | boolean | List affected folders instead of projects | `-l`, `--folders-only` |
| `workingDirectory` | string | Working directory for the analysis | `-d`, `--dir` |
| `verbose` | boolean | Enable verbose logging | `--verbose` |
| `timeoutMinutes` | number | Timeout for solution loading in minutes | `-t`, `--timeout` |
| `continueOnError` | boolean | Continue when command execution fails | `--continue-on-error` |
| `runInParallel` | boolean | Run commands in parallel | `--parallel` |
| `failOnNoProjects` | boolean | Fail if no projects are affected | `--fail-on-no-projects` |
| `noCache` | boolean | Ignore existing cache file | `--no-cache` |

## Sample Configuration File

Here's an example configuration file with all available settings:

```json
{
  "gitBranch": "master",
  "solutionFilePath": "MySolution.sln",
  "outputFile": "affected-projects.txt",
  "listFolders": false,
  "workingDirectory": null,
  "verbose": false,
  "timeoutMinutes": 2,
  "continueOnError": true,
  "runInParallel": false,
  "failOnNoProjects": false,
  "noCache": false
}
```

## Command-Line Override

Command-line arguments take precedence over configuration file settings. For example, if your configuration file specifies `"gitBranch": "master"` but you run `incrementalist --branch dev`, the `dev` branch will be used.

## Usage Examples

### Basic Usage

1. Create an `incrementalist.json` file in your project root:

```json
{
  "gitBranch": "master",
  "solutionFilePath": "MySolution.sln",
  "verbose": true
}
```

2. Run Incrementalist without specifying these options on the command line:

```bash
dotnet run -- --run -- build
```

### Using a Different Configuration File

```bash
dotnet run -- --config my-custom-config.json --run -- build
```

### Overriding Configuration Values

```bash
dotnet run -- --branch dev --verbose false --run -- build
```

This will use the `dev` branch and disable verbose logging, overriding any values in the configuration file. 