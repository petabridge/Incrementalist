#### 1.1.0-beta1 May 12 2025 ####

**Major New Features & Improvements:**

* **Custom Process Execution with `run-process` Verb:**  
  You can now use the new `run-process` verb to run any process (not just `dotnet`) against affected projects. This enables advanced scenarios, such as running custom scripts or tools before or after your build/test steps.  

  _Example:_  
  
  ```shell
  incrementalist run-process --process /bin/bash -- echo 'Hello from Incrementalist!'
  ```

* **Significant Performance Boost with Static Graph Engine:**  
  Incrementalist now uses the MSBuild Static Graph engine by default for solution and project parsing. This change should make Incrementalist _significantly_ faster, especially on large solutions. The previous engine is still available via `--engine Workspace` for compatibility and comparison.

* **Dependency Updates:**  
  - Updated `xunit.runner.visualstudio` to 3.1.0 for improved .NET 8 compatibility and bug fixes.

**Bug Fixes:**

* Fixed running tests on macOS by resolving symlink issues in temporary directories.
* Improved project dependency detection logic for solutions with multiple target frameworks.
* (Temporary) Reverted a previous dependency detection fix due to downstream issues.

**Documentation:**

* Added a new page with real-world usage examples.

#### 1.0.0 April 17 2025 ####

Incrementalist v1.0.0 is here! This release marks a significant step forward, introducing powerful new features focused on usability, performance, and integration with modern .NET development workflows.

**Major New Features:**

*   **Built-in `dotnet` Command Execution:** Directly execute `dotnet` CLI commands on projects affected by changes since a specified base revision (e.g., `main` or `dev`). Use the `-r` or `--run` flag followed by your standard `dotnet` command.

    ```shell
    # Build only affected projects compared to the 'dev' branch
    incrementalist -b dev -r -- build -c Release --nologo

    # Run tests for affected projects in parallel
    incrementalist -b dev -r --parallel -- test -c Release --no-build --nologo
    ```

*   **File-based Configuration (`.incrementalist.yml`):** Configure Incrementalist using a YAML file instead of command-line arguments. Store common settings, project filters, and default branches.

    ```yaml
    # .incrementalist.yml
    base-branch: dev
    skip-glob: "**/obj/**,**/bin/**"
    target-glob: "src/**/*.csproj"
    log-level: Information
    run: build -c Release
    parallel: true
    ```

    Load the configuration: `incrementalist -c .incrementalist.yml`

*   **File Globbing for Commands (`--glob`, `--skip-glob`, `--target-glob`):** Precisely target or exclude projects for command execution using glob patterns. This allows for fine-grained control over which affected projects specific commands are run against.

    ```shell
    # Run tests only on affected *.Tests.csproj projects
    incrementalist -b dev -r --target-glob "**/*.Tests.csproj" -- test
    
    # Build all affected projects EXCEPT those in the 'samples' directory
    incrementalist -b dev -r --skip-glob "**/samples/**/*.csproj" -- build
    ```

*   **Command-Line Verbs:** Reorganized command-line arguments using verbs for better structure and clarity (Breaking Change introduced in `1.0.0-rc4`). See documentation for updated commands.

**Improvements:**

*   Improved detection of changes in shared MSBuild files like `Directory.Build.props` and imported `.props`/`.targets` files.
*   Enhanced project dependency analysis for more accurate calculation of affected projects.
*   Improved logging system with configurable levels and better context.
*   More robust quoting for arguments passed to `dotnet` commands.
*   Added support for detecting unstaged file changes in Git.
*   Added `IProgress<ProjectLoadProgress>` support for MSBuild loading.
*   Updated documentation with examples for configuration files and globbing.
*   Upgraded dependencies (Roslyn, NuGet, Microsoft.Extensions.Logging, etc.) to latest versions.

**Bug Fixes:**

*   Resolved issues with configuration-based `SkipGlob` and `TargetGlob` being overwritten by unspecified CLI arguments ([#402](https://github.com/petabridge/Incrementalist/issues/402)).
*   Fixed globbing pattern application when a full solution build is required ([#395](https://github.com/petabridge/Incrementalist/issues/395)).
*   Corrected dependency graph calculation errors ([#389](https://github.com/petabridge/Incrementalist/issues/389)).
*   Fixed globbing with absolute paths ([#386](https://github.com/petabridge/Incrementalist/issues/386)).
*   Resolved issues with command-line option parsing, including duplicate definitions and `--create-config` behavior ([#379](https://github.com/petabridge/Incrementalist/pull/379), [#382](https://github.com/petabridge/Incrementalist/pull/382)).
*   Fixed various issues related to command execution, parallelism, and error handling (`--continue-on-error`, `--fail-on-no-projects`).
*   Fixed NuGet packaging metadata ([#337](https://github.com/petabridge/Incrementalist/pull/337)).
*   Corrected logging of `null` values during default config loading ([#401](https://github.com/petabridge/Incrementalist/pull/401)).