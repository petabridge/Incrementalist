---
name: repo
type: repo
agent: CodeActAgent
---

Repository: Incrementalist
Description: Incrementalist is a `dotnet tool` that leverages libgit2sharp and Roslyn to compute incremental build steps to help reduce total build time in CI/CD for large .NET solutions.

Please see `README.md` for a full recounting of the most recently supported Incrementalist CLI instructions et al.

Directory Structure:
- src/Incrementalist: The main Incrementalist library
- src/Incrementalist.Cmd: The `dotnet tool` / CLI for invoking Incrementalist
- src/Incrementalist.Tests: Test files
- build-system: The Azure DevOps YAML we use to manage builds

Setup:
- Run `dotnet build` to build the project
- Use `dotnet test -c Release` for testing
- Run `dotnet pack -c Release -o bin\nuget` for testing packaging of the solution

Guidelines:
- Write tests for all new features
- Any major changes in the CLI need to be documented in the `README.md`
- Make sure the `Incrementalist.Cmd` tooling supports .NET 6 and newer
- Make sure our standard copyright headers at the top of all files appear on any new files