# Incrementalist

Incrementalist is a .NET tool that leverages [libgit2sharp](https://github.com/libgit2/libgit2sharp/) and [Roslyn](https://docs.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/) to compute incremental build steps, to help reduce total build time, for Continuous Integration systems for _large_ .NET solutions.

## Installation and Use
Incrementalist is available in one of two distribution methods:

1. [Incrementalist Library](https://www.nuget.org/packages/Incrementalist/) - a stand-alone .NET Standard 2.0 NuGet package that can be called from a program of your own making or
2. [Incrementalist.Cmd](https://www.nuget.org/packages/Incrementalist.Cmd/) - a `dotnet tool` that can be run directly from the `dotnet` CLI. **Most users prefer `Incrementalist.Cmd` for running inside their build systems**.

To install Incrementalist and try for yourself, do the following:

```shell
dotnet tool install --global Incrementalist.Cmd
```

### `Incrementalist.Cmd` CLI Options

The following CLI options are available on Incrementalist, which you can print out at any time via the `incrementalist --help` command:

```
build - runs dotnet build on all of the affected projects
test - runs dotnet test on the affected projects, where relevant
pack - runs dotnet pack on the affected projects, where relevant

  -s, --sln             The name of the Solution file to be analyzed by Incrementalist.

  -c, --config          The MSBuild configuration to use. Defaults to "Debug."

  --framework           The .NET runtime to use, when more than one is specified.

  -f, --file            If specified, writes the output to the named file.

  -l, --folders-only    List affected folders instead of .NET projects

  -b, --branch          Required. (Default: dev) The git branch to compare against. i.e. the `dev` or the
                        `master` branch.

  -d, --dir             Specify the working directory explicitly. Defaults to using the current working
                        directory.

  --verbose             (Default: false) Prints out extensive debug logs during operation.

  -t, --timeout         (Default: 2) Specifies the load timeout for the solution in whole minutes.
                        Defaults to 2 minutes.

  --help                Display this help screen.

  --version             Display version information.
```

To run a standard Incrementalist build on a project like Akka.NET, we do the following:

```shell
PS> incrementalist -s ./src/Akka.sln -b dev --file ./bin/output/incrementalist.txt
```

## How It Works

Incrementalist works by analyizing the `git diff` of each commit in your working branch, comparing it to a base branch (i.e. `dev`) to determine which files have been modified in your changes, and then it uses Roslyn solution analysis to determine the graph of projects that were affected by these changes.

![Incrementalist - how it works](https://github.com/petabridge/Incrementalist/raw/dev/docs/images/incrementalist-how-it-works.png)

This graph analysis produces a text file that looks like this (when we're running it on the [Akka.NET main repository](https://github.com/akkadotnet/akka.net)):

> D:\a\1\s\src\core\Akka\Akka.csproj,D:\a\1\s\src\benchmark\SerializationBenchmarks\SerializationBenchmarks.csproj

* Each line represents one graph of changes detected by `git` and Roslyn - each project will be listed using its absolute path and will be separated by comma;
* If there are multiple lines in the output file, it means that multiple discrete graphs were detected (two sets of projects that don't directly relate to eachother were updated in the same set of `git` commits.)

This file can be parsed and used inside a build script, such as the [FAKE file](https://fake.build/) we use for running Akka.NET's build system:

```fsharp
//--------------------------------------------------------------------------------
// Incrementalist targets
//--------------------------------------------------------------------------------
// Pulls the set of all affected projects detected by Incrementalist from the cached file
let getAffectedProjectsTopology =
    lazy(
        log (sprintf "Checking inside %s for changes" incrementalistReport)

        let incrementalistFoundChanges = File.Exists incrementalistReport

        log (sprintf "Found changes via Incrementalist? %b - searched inside %s" incrementalistFoundChanges incrementalistReport)
        if not incrementalistFoundChanges then None
        else
            let sortedItems = (File.ReadAllLines incrementalistReport) |> Seq.map (fun x -> (x.Split ','))
                              |> Seq.map (fun items -> (items.[0], items))
            let d = dict sortedItems
            Some(d)
    )

let getAffectedProjects =
    lazy(
        let finalProjects = getAffectedProjectsTopology.Value
        match finalProjects with
        | None -> None
        | Some p -> Some (p.Values |> Seq.concat)
    )

Target "ComputeIncrementalChanges" (fun _ ->
    if runIncrementally then
        let targetBranch = match getBuildParam "targetBranch" with
                            | "" -> "dev"
                            | null -> "dev"
                            | b -> b
        let incrementalistPath =
                let incrementalistDir = toolsDir @@ "incrementalist"
                let globalTool = tryFindFileOnPath "incrementalist.exe"
                match globalTool with
                    | Some t -> t
                    | None -> if isWindows then findToolInSubPath "incrementalist.exe" incrementalistDir
                              elif isMacOS then incrementalistDir @@ "incrementalist"
                              else incrementalistDir @@ "incrementalist"


        let args = StringBuilder()
                |> append "-b"
                |> append targetBranch
                |> append "-s"
                |> append solution
                |> append "-f"
                |> append incrementalistReport
                |> toText

        let result = ExecProcess(fun info ->
            info.FileName <- incrementalistPath
            info.WorkingDirectory <- __SOURCE_DIRECTORY__
            info.Arguments <- args) (System.TimeSpan.FromMinutes 5.0) (* Reasonably long-running task. *)

        if result <> 0 then failwithf "Incrementalist failed. %s" args
    else
        log "Skipping Incrementalist - not enabled for this build"
)

let filterProjects selectedProject =
    if runIncrementally then
        let affectedProjects = getAffectedProjects.Value

        (*
        if affectedProjects.IsSome then
            log (sprintf "Searching for %s inside [%s]" selectedProject (String.Join(",", affectedProjects.Value)))
        else
            log "No affected projects found"
        *)

        match affectedProjects with
        | None -> None
        | Some x when x |> Seq.exists (fun n -> n.Contains (System.IO.Path.GetFileName(string selectedProject))) -> Some selectedProject
        | _ -> None
    else
        log "Not running incrementally"
        Some selectedProject
```

And in each one of our build steps where we want to execute incremental builds only (in order to reduce the amount of work per-pull request), we call the `filterProjects` method that uses the `Incrementalist.Cmd` output to determine which projects need to be considered based on these changes:

```fsharp
//--------------------------------------------------------------------------------
// Build targets
//--------------------------------------------------------------------------------
let skipBuild =
    lazy(
        match getAffectedProjects.Value with
        | None when runIncrementally -> true
        | _ -> false
    )

let headProjects =
    lazy(
        match getAffectedProjectsTopology.Value with
        | None when runIncrementally -> [||]
        | None -> [|solution|]
        | Some p -> p.Keys |> Seq.toArray
    )

Target "AssemblyInfo" (fun _ ->
    XmlPokeInnerText "./src/common.props" "//Project/PropertyGroup/VersionPrefix" releaseNotes.AssemblyVersion
    XmlPokeInnerText "./src/common.props" "//Project/PropertyGroup/PackageReleaseNotes" (releaseNotes.Notes |> String.concat "\n")
)

Target "Build" (fun _ ->
    if not skipBuild.Value then
        let additionalArgs = if versionSuffix.Length > 0 then [sprintf "/p:VersionSuffix=%s" versionSuffix] else []
        let buildProject proj =
            DotNetCli.Build
                (fun p ->
                    { p with
                        Project = proj
                        Configuration = configuration
                        AdditionalArgs = additionalArgs })

        match getAffectedProjects.Value with
        | Some p -> p |> Seq.iter buildProject
        | None -> buildProject solution // build the entire solution if incrementalist is disabled
)
```

This tool is best used for _large_ .NET projects, where the time to complete each build step can take 30+ minutes. Incrementalist can help reduce the average execution time by an order of magnitude for many pull requests.

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
