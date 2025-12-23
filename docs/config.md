# Incrementalist Configuration Files

Incrementalist now supports configuration files to store commonly used settings. This eliminates the need to specify the same command-line arguments repeatedly.

## Configuration File Format

Incrementalist uses a JSON-based configuration file format. By default, Incrementalist looks for a file named `incrementalist.json` in the `.incrementalist` directory, but you can specify a different file using the `-c` or `--config` command-line option.

### JSON Schema Support

Incrementalist provides a JSON schema that enables IDE IntelliSense, auto-completion, and validation for configuration files. To enable this feature, add the `$schema` property to your configuration file:

```json
{
  "$schema": "https://raw.githubusercontent.com/petabridge/Incrementalist/dev/src/Incrementalist.Cmd/Config/incrementalist.schema.json",
  "gitBranch": "master",
  "solutionFilePath": "MySolution.sln"
}
```

This will provide:
- **IntelliSense**: Auto-completion of property names and values in supported IDEs (VS Code, Visual Studio, JetBrains IDEs)
- **Validation**: Real-time error checking for invalid property names, types, and values
- **Documentation**: Hover tooltips with property descriptions and examples

## Available Settings

The following settings can be specified in the configuration file:

| Setting | Type | Description | CLI Equivalent |
|---------|------|-------------|----------------|
| `gitBranch` | string | The branch to compare against (e.g. "dev", "master") | `-b`, `--branch` |
| `solutionFilePath` | string | Path to the solution file to analyze | `-s`, `--sln` |
| `outputFile` | string | Path where affected projects will be written | `-f`, `--file` |
| `workingDirectory` | string | Working directory for the analysis | `-d`, `--dir` |
| `verbose` | boolean | Enable verbose logging | `--verbose` |
| `timeoutMinutes` | number | Timeout for solution loading in minutes | `-t`, `--timeout` |
| `continueOnError` | boolean | Continue when command execution fails | `--continue-on-error` |
| `runInParallel` | boolean | Run commands in parallel | `--parallel` |
| `parallelLimit` | number | Limit concurrent projects when running in parallel (0 = no limit) | `--parallel-limit` |
| `failOnNoProjects` | boolean | Fail if no projects are affected | `--fail-on-no-projects` |
| `skip` | string array | Glob patterns to exclude projects from the final list | `--skip-glob` |
| `target` | string array | Glob patterns to include only matching projects in the final list | `--target-glob` |
| `nameApplicationToStart` | string | The application or document to start the process | `--name-application-to-start` |

## Sample Configuration File

Here's an example configuration file with all available settings:

```json
{
  "$schema": "https://raw.githubusercontent.com/petabridge/Incrementalist/dev/src/Incrementalist.Cmd/Config/incrementalist.schema.json",
  "gitBranch": "master",
  "solutionFilePath": "MySolution.sln",
  "outputFile": "affected-projects.txt",
  "workingDirectory": null,
  "verbose": false,
  "timeoutMinutes": 2,
  "continueOnError": true,
  "runInParallel": false,
  "parallelLimit": 0,
  "failOnNoProjects": false,
  "noCache": false,
  "skip": ["**/bin/**", "**/obj/**"],
  "target": ["src/**/*.csproj"],
  "nameApplicationToStart": "dotnet"
}
```

## Command-Line Override

Command-line arguments take precedence over configuration file settings. For example, if your configuration file specifies `"gitBranch": "master"` but you run `incrementalist --branch dev`, the `dev` branch will be used.

## Usage Examples

### Basic Usage

1. Create an `incrementalist.json` file in your `.incrementalist` directory:

```json
{
  "$schema": "https://raw.githubusercontent.com/petabridge/Incrementalist/dev/src/Incrementalist.Cmd/Config/incrementalist.schema.json",
  "gitBranch": "master",
  "solutionFilePath": "MySolution.sln",
  "verbose": true
}
```

2. Run Incrementalist without specifying these options on the command line:

```bash
incrementalist run -- build
```

### Using a Different Configuration File

```bash
incrementalist run -c my-custom-config.json -- build
```

### Overriding Configuration Values

```bash
incrementalist run -b dev --verbose false -- build
```

This will use the `dev` branch and disable verbose logging, overriding any values in the configuration file.

## Creating Configuration Files

Incrementalist provides a dedicated verb to generate configuration files based on your current command-line options: `create-config`.

### Using the Default Path

By default, configuration files are created in the `.incrementalist` directory within your working directory:

```bash
incrementalist create-config -b master --verbose --parallel
```

This will create a file at `.incrementalist/incrementalist.json` containing all the specified options.

### Using a Custom File Name

You can specify a custom file name and location using the `-c` or `--config` option:

```bash
incrementalist create-config -b master --verbose --parallel -c ./my-config.json
```

This will create the configuration file at `./my-config.json` instead of the default location.

### Workflow Example

A typical workflow might be:

1. Create a configuration file with your commonly used settings:
   ```bash
   incrementalist create-config -b main --verbose --parallel
   ```

2. Use the configuration file for subsequent runs:
   ```bash
   incrementalist run -- build -c Release
   ```

3. Override specific settings when needed:
   ```bash
   incrementalist -b feature-branch run -- test
   ```

This approach allows you to maintain consistent settings while still having the flexibility to override them when necessary. 