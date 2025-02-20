#### 1.0.0-beta1 Feb 20 2025 ####

Major new feature: Built-in `dotnet` command execution support! You can now run commands directly on affected projects:
```shell
# Build only affected projects
incrementalist -b dev -r -- build -c Release --nologo

# Run tests for affected projects
incrementalist -b dev -r -- test -c Release --no-build --nologo

# Run in parallel for faster execution
incrementalist -b dev -r --parallel -- build -c Release --nologo
```

* Added command execution support with new options:
  * `-r, --run` - Run dotnet CLI commands against affected projects;
  * `--parallel` - Execute commands in parallel for faster builds;
  * `--continue-on-error` - Continue executing if some commands fail;
  * `--fail-on-no-projects` - Return error if no projects are affected;
* Improved MSBuild file detection:
  * Now properly detects changes in `Directory.Build.props`;
  * Better handling of shared MSBuild files and project dependencies;
* Enhanced project dependency analysis for more accurate incremental builds;
* Improved error handling and logging throughout; and
* Upgraded all dependencies to their latest stable versions.

#### 0.9.0 November 22 2023 ####

* Added .NET 8.0 support to `Incrementalist.Cmd`;
* Dropped .NET Core 3.1 support from `Incrementalist.Cmd`;
* Upgraded to Roslyn 4.8.0; and
* Upgraded to NuGet.ProjectModel 6.8.0.
