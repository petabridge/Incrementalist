#### 1.2.0-beta.1 October 31 2025 ####

**BETA RELEASE - .NET 9.0 and .slnx Support**

This is a beta release with upgraded dependencies to support the new XML-based .slnx solution format and .NET 9.0.

**Major Changes:**

* **Upgraded to .NET 9.0:**
  Updated target framework from net8.0 to net9.0 to support newer MSBuild and Roslyn dependencies.

* **Added .slnx Solution Format Support:**
  Full support for the new XML-based .slnx solution format in both Workspace and Static Graph build engines.

* **Dependency Upgrades:**
  - Roslyn (Microsoft.CodeAnalysis): 4.14.0 → 5.0.0-2.final (prerelease)
  - MSBuild: 17.11.48 → 17.14.28
  - Added Microsoft.CodeAnalysis.Common package

* **Improved F# Project Handling:**
  F# projects now use StaticGraphBuildEngine for better compatibility with Roslyn 5.0+.

**Breaking Changes:**

* Requires .NET 9.0 SDK or later
* Uses prerelease Roslyn dependency (5.0.0-2.final) until stable Roslyn 5.0 is released

**Bug Fixes:**

* Fixed deprecated Workspace.WorkspaceFailed API usage
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