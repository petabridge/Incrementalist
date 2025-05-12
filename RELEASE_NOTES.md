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