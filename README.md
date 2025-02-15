# Incrementalist

Incrementalist is a .NET tool that leverages [libgit2sharp](https://github.com/libgit2/libgit2sharp/) and [Roslyn](https://docs.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/) to compute incremental build steps, to help reduce total build time, for Continuous Integration systems for _large_ .NET solutions.

## Installation and Use
Incrementalist is available in one of two distribution methods:

1. [Incrementalist Library](https://www.nuget.org/packages/Incrementalist/) - a .NET 8 library that can be called from a program of your own making or
2. [Incrementalist.Cmd](https://www.nuget.org/packages/Incrementalist.Cmd/) - a `dotnet tool` that can be run directly from the `dotnet` CLI. **Most users prefer `Incrementalist.Cmd` for running inside their build systems**.

To install Incrementalist and try for yourself, do the following:

```shell
dotnet tool install --global Incrementalist.Cmd
```

### Quick Start Examples

Here are some common use cases to get you started:

```shell
# Get list of affected projects and save to file (for use in build scripts)
incrementalist -s ./src/MySolution.sln -b dev -f ./affected-projects.txt

# Get list of affected folders and save to file
incrementalist -s ./src/MySolution.sln -b dev -l -f ./affected-folders.txt

# Build only the affected projects in Release configuration
incrementalist -s ./src/MySolution.sln -b dev -r -- build -c Release --nologo

# Run tests only for affected projects
incrementalist -s ./src/MySolution.sln -b dev -r -- test -c Release --no-build --nologo

# Run tests with code coverage collection
incrementalist -s ./src/MySolution.sln -b dev -r -- test -c Release --no-build --nologo /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:CoverletOutput=./coverage/

# Save affected projects to file AND run commands against them
incrementalist -s ./src/MySolution.sln -b dev -f ./affected-projects.txt -r -- build -c Release --nologo
```

### Output Files
Incrementalist can generate two types of output files using the `-f, --file` option:

1. **Project Lists** (default mode): Contains comma-separated lists of affected .NET project files
   ```
   D:\src\Project1\Project1.csproj,D:\src\Project2\Project2.csproj
   ```

2. **Folder Lists** (with `-l, --folders-only`): Contains comma-separated lists of affected folders
   ```
   D:\src\Project1,D:\src\Project2\SubFolder
   ```

These output files can be used in build scripts, CI/CD pipelines, or any other automation tools. You can combine file output with command execution (`-r`) to both save the affected items list and run commands against them. Capturing these files as build artifacts can be useful for understanding what dependency graphs the tool detected and executed.

### `Incrementalist.Cmd` CLI Options

The following CLI options are available on Incrementalist, which you can print out at any time via the `incrementalist --help` command:

```
  -s, --sln             The name of the Solution file to be analyzed by Incrementalist.

  -f, --file            If specified, writes the output to the named file.

  -l, --folders-only    List affected folders instead of .NET projects

  -b, --branch          Required. (Default: dev) The git branch to compare against. i.e. the `dev` or the
                        `master` branch.

  -d, --dir             Specify the working directory explicitly. Defaults to using the current working
                        directory.

  --verbose             (Default: false) Prints out extensive debug logs during operation.

  -t, --timeout         (Default: 2) Specifies the load timeout for the solution in whole minutes.
                        Defaults to 2 minutes.

  -r, --run            Run a dotnet CLI command against affected projects. All arguments after -- will be 
                       passed to dotnet.

  --continue-on-error   (Default: true) When running commands, continue executing even if some commands fail.

  --parallel           (Default: false) When running commands, execute them in parallel.

  --help                Display this help screen.

  --version             Display version information.
```

To run a standard Incrementalist build on a project like Akka.NET, we do the following:

```shell
PS> incrementalist -s ./src/Akka.sln -b dev --file ./bin/output/incrementalist.txt
```

### Running Commands Against Affected Projects

Incrementalist can now execute `dotnet` CLI commands against affected projects. This is useful for running builds, tests, or other commands only on the projects that have changed. Here are some examples:

```shell
# Build affected projects
incrementalist -s ./src/MySolution.sln -b dev -r -- build -c Release --nologo

# Run tests on affected projects
incrementalist -s ./src/MySolution.sln -b dev -r -- test -c Release --no-build --nologo

# Run multiple commands in parallel
incrementalist -s ./src/MySolution.sln -b dev -r --parallel -- build -c Release --nologo

# Stop on first error
incrementalist -s ./src/MySolution.sln -b dev -r --continue-on-error=false -- build -c Release --nologo
```

## How It Works

Incrementalist works by analyzing the `git diff` of each commit in your working branch, comparing it to a base branch (i.e. `dev`) to determine which files have been modified in your changes, and then it uses Roslyn solution analysis to determine the graph of projects that were affected by these changes.

![Incrementalist - how it works](https://github.com/petabridge/Incrementalist/raw/dev/docs/images/incrementalist-how-it-works.png)

This graph analysis produces a text file that looks like this (when we're running it on the [Akka.NET main repository](https://github.com/akkadotnet/akka.net)):

> D:\a\1\s\src\core\Akka\Akka.csproj,D:\a\1\s\src\benchmark\SerializationBenchmarks\SerializationBenchmarks.csproj

* Each line represents one graph of changes detected by `git` and Roslyn - each project will be listed using its absolute path and will be separated by comma;
* If there are multiple lines in the output file, it means that multiple discrete graphs were detected (two sets of projects that don't directly relate to each other were updated in the same set of `git` commits.)

This file can be parsed and used inside a build script, such as the [FAKE file](https://fake.build/) we use for running Akka.NET's build system.

This tool is best used for _large_ .NET projects, where the time to complete each build step can take 30+ minutes. Incrementalist can help reduce the average execution time by an order of magnitude for many pull requests.

## Requirements

- .NET 8.0 SDK or later
- Git installed and available in the system PATH

## Build Instructions
To run the build script associated with this solution, execute the following:

**Windows**
```
c:\> build.cmd all
```

**Linux / OS X**
```
c:\> build.sh all
```

If you need any information on the supported commands, please execute the `build.[cmd|sh] help` command.

This build script is powered by [FAKE](https://fake.build/); please see their API documentation should you need to make any changes to the [`build.fsx`](build.fsx) file.

### Conventions
The attached build script will automatically do the following based on the conventions of the project names added to this project:

* Any project name ending with `.Tests` will automatically be treated as a [XUnit2](https://xunit.github.io/) project and will be included during the test stages of this build script;
* Any project name ending with `.Tests` will automatically be treated as a [NBench](https://github.com/petabridge/NBench) project and will be included during the test stages of this build script; and
* Any project meeting neither of these conventions will be treated as a NuGet packaging target and its `.nupkg` file will automatically be placed in the `bin\nuget` folder upon running the `build.[cmd|sh] all` command.

### DocFx for Documentation
This solution also supports [DocFx](http://dotnet.github.io/docfx/) for generating both API documentation and articles to describe the behavior, output, and usages of your project. 

All of the relevant articles you wish to write should be added to the `/docs/articles/` folder and any API documentation you might need will also appear there.

All of the documentation will be statically generated and the output will be placed in the `/docs/_site/` folder. 

#### Previewing Documentation
To preview the documentation for this project, execute the following command at the root of this folder:

```
C:\> serve-docs.cmd
```

This will use the built-in `docfx.console` binary that is installed as part of the NuGet restore process from executing any of the usual `build.cmd` or `build.sh` steps to preview the fully-rendered documentation. For best results, do this immediately after calling `build.cmd buildRelease`.

### Release Notes, Version Numbers, Etc
This project will automatically populate its release notes in all of its modules via the entries written inside [`RELEASE_NOTES.md`](RELEASE_NOTES.md) and will automatically update the versions of all assemblies and NuGet packages via the metadata included inside [`Directory.Build.props`](src/Directory.Build.props).

### Code Signing via SignService
This project uses [SignService](https://github.com/onovotny/SignService) to code-sign NuGet packages prior to publication. The `build.cmd` and `build.sh` scripts will automatically download the `SignClient` needed to execute code signing locally on the build agent, but it's still your responsibility to set up the SignService server per the instructions at the linked repository.

Once you've gone through the ropes of setting up a code-signing server, you'll need to set a few configuration options in your project in order to use the `SignClient`:

* Add your Active Directory settings to [`appsettings.json`](appsettings.json) and
* Pass in your signature information to the `signingName`, `signingDescription`, and `signingUrl` values inside `build.fsx`.

Whenever you're ready to run code-signing on the NuGet packages published by `build.fsx`, execute the following command:

```
C:\> build.cmd nuget SignClientSecret={your secret} SignClientUser={your username}
```

This will invoke the `SignClient` and actually execute code signing against your `.nupkg` files prior to NuGet publication.

If one of these two values isn't provided, the code signing stage will skip itself and simply produce unsigned NuGet code packages.
