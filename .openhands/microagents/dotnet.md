---
name: repo
type: repo
agent: CodeActAgent
triggers:
- dotnet
- global.json
---

If we need to run the `dotnet` CLI, please make sure it's installed inside the environment.

You can detect which versions of `dotnet` are available by running `dotnet --list-sdks`.

Generally, it's a good idea to use a `global.json` file to control the version of .NET being used in builds - and we always like to set the "RollFoward" parameter to `LatestMinor`.