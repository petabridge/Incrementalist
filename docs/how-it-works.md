# How Incrementalist Works

Incrementalist is designed to optimize build processes in large .NET solutions by intelligently determining which projects need to be rebuilt based on changes in your Git repository.

## Architecture Overview

Incrementalist combines several key technologies to provide accurate incremental build analysis:

1. **Git Integration** - Uses `libgit2sharp` to analyze repository changes
2. **Solution Analysis** - Leverages Roslyn to understand project dependencies
3. **Build Analysis** - Determines whether changes require full or incremental builds

## Core Components

### Git Analysis
- Compares working branch against a target branch (e.g., `dev` or `master`)
- Identifies all modified files using `git diff`
- Handles various Git scenarios including remote branches and detached HEAD states

### Solution Analysis
- Parses .NET solution and project files using Roslyn
- Builds a dependency graph of all projects
- Identifies projects affected by file changes
- Handles both direct project file changes and imported MSBuild files

### Change Detection
Incrementalist employs sophisticated change detection that considers:

1. **Solution-Wide Changes**
   - Changes to `Directory.Build.props`
   - Changes to `Directory.Packages.props`
   - Changes to `global.json`
   - Changes to solution files
   - Widely imported MSBuild files

2. **Project-Specific Changes**
   - Direct changes to project files
   - Changes to source files
   - Changes to project dependencies

### Output Generation

The analysis produces a list of affected projects or folders. For example:

```
D:\src\Project1\Project1.csproj,D:\src\Project2\Project2.csproj
```

Each line represents a graph of related changes:
- Projects are listed with absolute paths
- Multiple lines indicate discrete change graphs
- Can be formatted as project paths or folder paths

![Incrementalist - how it works](https://github.com/petabridge/Incrementalist/raw/dev/docs/images/incrementalist-how-it-works.png)

## Command Execution

When running commands against affected projects, Incrementalist:

1. Analyzes the solution to determine affected projects
2. Executes specified `dotnet` commands against each project
3. Supports parallel execution for faster processing
4. Provides configurable error handling

## Integration

The output can be integrated with various build systems:
- CI/CD pipelines
- Build scripts (e.g., FAKE, CAKE, etc.)
- Custom build tooling

For detailed build instructions and setup, see [Building Incrementalist](building.md). 