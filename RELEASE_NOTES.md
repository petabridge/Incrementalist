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
