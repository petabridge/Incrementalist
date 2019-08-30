#### 0.1.6 August 30 2019 ####
Bugfix release for Incrementalist v0.1.4-v0.1.5

Fixed [Bug: doesn't detect that project has changed when embedded resource has been modified](https://github.com/petabridge/Incrementalist/issues/56).

As it turns out, Roslyn isn't able to detect non-code files embedded as resources into projects - so we search to see if any modified files are contained in the same folder as solution projects and we'll now mark the project as updated in the event that it contains a modified file.

Fixed [issue with detecting transitive dependencies in multi-targeted builds](https://github.com/petabridge/Incrementalist/issues/55).