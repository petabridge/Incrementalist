#### 1.2.1 May 28 2026 ####

**Bug Fixes:**

* **MSBuild Locator Type Mismatch on .NET 10.x:**
  Fixed MSBuild assembly type mismatch that caused Incrementalist to fail on .NET 10.x SDKs. The MSBuild.Locator now properly discovers SDK 10's MSBuild 18.x assemblies by scanning .NET SDK paths instead of only looking for Visual Studio installations.

**Release Infrastructure:**

* Extracted the latest release block logic for GitHub release body generation, making release automation more reliable

Fixes #513

#### 1.2.0 December 23 2025 ####

**Stable Release - Parallel Execution Control**

This stable release adds a new feature to control parallelism during build and test operations.

**New Features:**

* **Parallel Limit Control:**
  Added `--parallel-limit` CLI option and `parallelLimit` configuration setting to limit the degree of parallelism when running tasks in parallel. This helps prevent memory exhaustion and random errors when many projects are affected in large solutions.

  _Example:_

  ```shell
  incrementalist test --parallel --parallel-limit 4
  ```

  Or in `incrementalist.json`:

  ```json
  {
    "parallelLimit": 4
  }
  ```

**Documentation:**

* Added comprehensive documentation for `--parallel-limit` option in README.md and configuration docs
* Updated JSON schema with validation for parallelLimit setting
* Improved README.md Markdown compatibility for better NuGet.org rendering

**Other Improvements:**

* Increased PR validation timeout from 10 to 20 minutes to accommodate integration tests on Windows

Fixes #456

#### 1.2.0-beta.1 December 15 2025 ####

**BETA RELEASE - Dual-Targeting .NET 8.0 and .NET 10.0 with .slnx Support**

This beta release adds dual-targeting support for .NET 8.0 and .NET 10.0, with .slnx solution format support available on .NET 10.0+.

**Major Changes:**

* **Dual-Targeting .NET 8.0 and .NET 10.0:**
  Incrementalist now targets both net8.0 and net10.0, allowing it to run on either .NET 8.0 or .NET 10.0 SDKs.

* **Added .slnx Solution Format Support (.NET 10.0+ only):**
  Full support for the new XML-based .slnx solution format in both Workspace and Static Graph build engines when running on .NET 10.0+. This format is not available on .NET 8.0 due to MSBuild/Roslyn version constraints.

* **TFM-Conditional Dependency Versions:**
  MSBuild and Roslyn package versions are now conditional based on the target framework:
  - .NET 8.0: MSBuild 17.11.48, Roslyn 4.14.0
  - .NET 10.0: MSBuild 18.0.2, Roslyn 5.0.0 (stable)

* **Multi-Targeted Project Deduplication:**
  Fixed issue where multi-targeted projects could appear multiple times in the dependency graph. Projects are now properly deduplicated in StaticGraphBuildEngine.

* **Improved F# Project Handling:**
  F# projects now use StaticGraphBuildEngine for better compatibility with Roslyn 5.0+.

* **Dependency Upgrades:**
  - Microsoft.Build.Locator: 1.9.1 → 1.10.12
  - xunit.runner.visualstudio: 3.1.4 → 3.1.5

**Bug Fixes:**

* Fixed deprecated Workspace.WorkspaceFailed API usage (replaced with RegisterWorkspaceFailedHandler)
* Updated F# test samples to use net8.0 target framework

Fixes #405

#### 1.1.0 September 3 2025 ####

**Major New Features & Improvements:**

* **Custom Process Execution with `run-process` Verb:**  
  You can now use the new `run-process` verb to run any process (not just `dotnet`) against affected projects. This enables advanced scenarios, such as running custom scripts or tools before or after your build/test steps.  

  _Example:_  

  ```shell
  incrementalist run-process --process /bin/bash -- echo 'Hello from Incrementalist!'
  ```

* **Significant Performance Boost with Static Graph Engine:**  
  Incrementalist now uses the MSBuild Static Graph engine by default for solution and project parsing. This change should make Incrementalist _significantly_ faster, especially on large solutions. The previous engine is still available via `--engine Workspace` for compatibility and comparison.

* **JSON Schema for Configuration Files:**  
  Added JSON schema support for `incrementalist.json` configuration files, enabling IDE IntelliSense and validation. This makes it easier to write and maintain configuration files with autocomplete and error checking.

**Bug Fixes:**

* Fixed running tests on macOS by resolving symlink issues in temporary directories.
* Improved project dependency detection logic for solutions with multiple target frameworks.
* (Temporary) Reverted a previous dependency detection fix due to downstream issues.

**Documentation:**

* Added a new page with real-world usage examples.