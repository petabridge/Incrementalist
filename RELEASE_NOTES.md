#### 1.0.0-rc1 Apr 13 2025 ####

Added major new features to enhance usability and extend capabilities:

* **File-based Configuration**: Added support for `.incrementalist.yml` configuration files. Store common settings and project filters in a single file rather than passing command-line arguments. Example: `incrementalist -c .incrementalist.yml` loads all settings from the file.

* **File Globbing Support**: Added file globbing pattern support for command execution. Target files using glob patterns for selective builds or testing. Example: `incrementalist -b dev -r --glob "**/*.Tests.csproj" -- test` runs tests only on affected test projects.

* **`.slnx` Support**: Added support for [modern Visual Studio solution files](https://devblogs.microsoft.com/dotnet/introducing-slnx-support-dotnet-cli/) (`.slnx`). Incrementalist now properly processes these files alongside standard `.sln` files, enabling better integration with Visual Studio's "Open Folder" feature.

All changes:

* [Enable `Nullability` and `TreatWarningsAsErrors`](https://github.com/petabridge/Incrementalist/pull/372)
* [Added support for file globbing commands](https://github.com/petabridge/Incrementalist/pull/371)
* [Added `.slnx` support](https://github.com/petabridge/Incrementalist/pull/370)
* [Solution cleanup](https://github.com/petabridge/Incrementalist/pull/369)
* [Disable caching](https://github.com/petabridge/Incrementalist/pull/367)
* [File-based configuration format](https://github.com/petabridge/Incrementalist/pull/357)
* [Bump Microsoft.Extensions.Logging.Console from 9.0.3 to 9.0.4](https://github.com/petabridge/Incrementalist/pull/364)
* [Bump Microsoft.Build.Locator from 1.7.8 to 1.9.1](https://github.com/petabridge/Incrementalist/pull/362)
* [Bump Microsoft.Extensions.Logging from 9.0.2 to 9.0.3](https://github.com/petabridge/Incrementalist/pull/361)
* [Bump Microsoft.Extensions.Logging.Console from 9.0.2 to 9.0.3](https://github.com/petabridge/Incrementalist/pull/360)
* [Bump NuGet.ProjectModel from 6.13.1 to 6.13.2](https://github.com/petabridge/Incrementalist/pull/359)

#### 1.0.0-beta4 Feb 25 2025 ####

* [Added support for `IProgress<ProjectLoadProgress>` to MSBuild](https://github.com/petabridge/Incrementalist/pull/353)
* [Fix cli parsing](https://github.com/petabridge/Incrementalist/pull/349)
* [Bumped Roslyn to 4.13.0](https://github.com/petabridge/Incrementalist/pull/351)

#### 1.0.0-beta3 Feb 24 2025 ####

* [Add more robust quoting for `dotnet` commands](https://github.com/petabridge/Incrementalist/pull/347)

#### 1.0.0-beta2 Feb 21 2025 ####

* Added graph caching to prevent full Roslyn analysis every time: https://github.com/petabridge/Incrementalist/blob/dev/docs/caching.md
* Added support for detecting unstaged file changes [#331](https://github.com/petabridge/Incrementalist/pull/331)
* Improved logging system [#336](https://github.com/petabridge/Incrementalist/pull/336) 
* Fixed NuGet metadata [#337](https://github.com/petabridge/Incrementalist/pull/337)

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
