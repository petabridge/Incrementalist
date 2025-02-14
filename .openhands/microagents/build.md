---
name: repo
type: repo
agent: CodeActAgent
triggers:
- build
- azure devops
- YAML
- dotnet
- powershell
- bash
- FAKE
---

If we are making changes to the build system, make sure those changes are always reflected in the `*.YAML` files that run the CI/CD for this repository. They are usually found in:

- `build-system`
- `.gitub`
- `.azure`

Make sure you test all of the instructions we give to the CI/CD system locally before making any changes.

Avoid writing large scripts to run CI/CD - we prefer a YAML file that is composed of simpler instructions and individual component parts. Only write a script when you need to do something more complicated than calling a simple instruction.

And when writing build system scripts, use PowerShell and have the CI/CD system use the cross-platform `pwsh` interpreter so PowerShell can run on Linux too.