# How Incrementalist Works

Incrementalist is designed to optimize build processes in large .NET solutions by intelligently determining which projects need to be rebuilt based on changes in your Git repository.

## Basic Workflow

```mermaid
flowchart TD
    A[Your Git Changes] --> B[Incrementalist]
    B --> C{Analyze Changes}
    C -->|Solution-wide changes| D[Build All Projects]
    C -->|Specific changes| E[Build Affected Projects]
    
    style B fill:#f9f,stroke:#333,stroke-width:4px
```

## Detailed Analysis Process

```mermaid
flowchart LR
    A[Git Changes] --> B[Check Files]
    B --> C{Solution-wide?}
    C -->|Yes| D[Full Build]
    C -->|No| E[Project Analysis]
    E --> F[Find Dependencies]
    F --> G[Generate Build List]
    
    subgraph "Solution-wide Changes"
        H[Directory.Build.props]
        I[global.json]
        J[.sln files]
    end
    
    H --> C
    I --> C
    J --> C
```

## Command Execution Flow

```mermaid
flowchart TD
    A[Incrementalist Command] --> B{Analysis Type}
    B -->|Project List| C[List Affected Projects]
    B -->|Folder List| D[List Affected Folders]
    B -->|Run Command| E[Execute dotnet Commands]
    
    C --> F[Save to File]
    D --> F
    E --> G[Build/Test Projects]
    
    style A fill:#f96,stroke:#333,stroke-width:4px
```

## Core Components

### Git Analysis
- Compares your current changes against a target branch (e.g., `dev` or `master`)
- Identifies all modified files
- Determines if changes require a full solution build

### Solution Analysis
- Understands your project dependencies
- Identifies which projects are affected by your changes
- Ensures all necessary projects are included in the build

### Output Generation

Incrementalist can produce two types of outputs:

1. **Project Lists** (default):
   ```
   D:\src\Project1\Project1.csproj,D:\src\Project2\Project2.csproj
   ```

2. **Folder Lists** (with `-l` option):
   ```
   D:\src\Project1,D:\src\Project2\SubFolder
   ```

## Command Execution

When running commands against affected projects, Incrementalist:

1. Analyzes your changes to determine affected projects
2. Executes specified `dotnet` commands against each project
3. Can run commands in parallel for faster processing
4. Provides configurable error handling

## Integration

The output can be integrated with various build systems:
- CI/CD pipelines
- Build scripts (e.g., FAKE, CAKE, etc.)
- Custom build tooling

For detailed build instructions and setup, see [Building Incrementalist](building.md). 