---
name: repo
type: repo
agent: CodeActAgent
triggers:
- NuGet
- NuGet.config
- Directory.Package.props
- csproj
- fsproj
- package
---

If you need to install, update, or remove a NuGet package from a .NET solution - always check to see if there's a `Directory.Package.props` is available in the folder structure somewhere - usually above where any of the relevant .csproj or .fsproj files are. If so, this means that we never try to set a package's version directly inside a `.csproj`. We always update the `Directory.Package.props` instead.

If the solution doesn't have a `Directory.Package.props` always suggest adding one and centralize all dependencies in it. `Directory.Package.props` with central package management enabled always requires a `NuGet.config` file - please add one of those too.

If you are adding a `Directory.Package.props` file to a solution that didn't previously have one - and there are multiple projects each referring to different versions of the same package, then use the highest version as the "default" version for the `Directory.Package.props`