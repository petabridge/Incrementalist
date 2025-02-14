#!/usr/bin/env pwsh
param(
    [Parameter(Position = 0)]
    [string] $Target = "Help",
    [string] $Configuration = "Release",
    [string] $NugetApiKey = "",
    [string] $NugetSource = "",
    [string] $SignClientUser = "",
    [string] $SignClientSecret = "",
    [string] $PreReleaseTag = ""
)

$ErrorActionPreference = "Stop"

# Detect TeamCity
$BuildNumber = $env:BUILD_NUMBER
if (-not $BuildNumber) { $BuildNumber = "0" }
$IsTeamCity = $BuildNumber -ne "0"

# Version information
$ReleaseNotes = Get-Content -Path "./RELEASE_NOTES.md" | Select-Object -First 1
if ($ReleaseNotes -match "^\*\*(.+?)\*\*") {
    $Version = $matches[1]
} else {
    throw "Version not found in RELEASE_NOTES.md"
}

$PreReleaseSuffix = if ($PreReleaseTag) {
    $PreReleaseTag
} elseif ($BuildNumber -ne "0") {
    "beta$BuildNumber"
} else {
    ""
}

# Directories
$SolutionPath = Get-ChildItem -Path "*.sln" | Select-Object -First 1
$OutputDir = Join-Path $PSScriptRoot "bin"
$TestResultsDir = Join-Path $PSScriptRoot "TestResults"
$PerfResultsDir = Join-Path $PSScriptRoot "PerfResults"
$NugetOutputDir = Join-Path $OutputDir "nuget"

# Ensure .NET local tools are installed
function EnsureTools {
    if (-not (Test-Path ".config/dotnet-tools.json")) {
        dotnet new tool-manifest
        if ($LASTEXITCODE -ne 0) { throw "Failed to create tool manifest" }
    }
    
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw "Failed to restore .NET tools" }
}

function Clean {
    if (Test-Path $OutputDir) { Remove-Item -Recurse -Force $OutputDir }
    if (Test-Path $TestResultsDir) { Remove-Item -Recurse -Force $TestResultsDir }
    if (Test-Path $PerfResultsDir) { Remove-Item -Recurse -Force $PerfResultsDir }
    if (Test-Path "docs/_site") { Remove-Item -Recurse -Force "docs/_site" }
}

function UpdateAssemblyInfo {
    $propsPath = "./src/Directory.Build.props"
    $xml = [xml](Get-Content $propsPath)
    $versionPrefix = $xml.SelectSingleNode("//Project/PropertyGroup/VersionPrefix")
    $releaseNotes = $xml.SelectSingleNode("//Project/PropertyGroup/PackageReleaseNotes")
    
    $versionPrefix.InnerText = $Version
    $releaseNotes.InnerText = (Get-Content -Path "./RELEASE_NOTES.md" | Select-Object -Skip 1) -join "`n"
    
    $xml.Save($propsPath)
}

function Build {
    dotnet restore $SolutionPath
    if ($LASTEXITCODE -ne 0) { throw "Restore failed" }

    dotnet build $SolutionPath -c $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Build failed" }
}

function RunTests {
    $testProjects = Get-ChildItem -Path "src" -Recurse -Filter "*.Tests.csproj"
    foreach ($project in $testProjects) {
        $loggerArgs = if ($IsTeamCity) {
            "--logger:trx --logger:""console;verbosity=normal"" --results-directory $TestResultsDir -- -parallel none -teamcity"
        } else {
            "--logger:trx --logger:""console;verbosity=normal"" --results-directory $TestResultsDir -- -parallel none"
        }
        
        dotnet test $project.FullName -c $Configuration --no-build $loggerArgs
        if ($LASTEXITCODE -ne 0) { throw "Tests failed for $($project.Name)" }
    }
}

function RunIntegrationTests {
    $frameworks = @("net6.0", "net7.0", "net8.0")
    $cmdProject = Get-ChildItem -Path "src" -Recurse -Filter "Incrementalist.Cmd.csproj" | Select-Object -First 1

    foreach ($framework in $frameworks) {
        # Folders-only check
        $folderArgs = "run --project $($cmdProject.FullName) -c $Configuration --framework $framework --no-build -- -b dev -l -f $TestResultsDir/incrementalist-affected-folders.txt"
        dotnet $folderArgs
        if ($LASTEXITCODE -ne 0) { throw "Integration test (folders) failed for $framework" }

        # Solution check
        $slnArgs = "run --project $($cmdProject.FullName) -c $Configuration --framework $framework --no-build -- -b dev -f $TestResultsDir/incrementalist-affected-files.txt"
        dotnet $slnArgs
        if ($LASTEXITCODE -ne 0) { throw "Integration test (solution) failed for $framework" }
    }
}

function RunNBenchTests {
    $perfProjects = Get-ChildItem -Path "src" -Recurse -Filter "*.Tests.Performance.csproj"
    foreach ($project in $perfProjects) {
        $nbenchArgs = if ($IsTeamCity) {
            "nbench --nobuild --teamcity --concurrent true --trace true --output $PerfResultsDir"
        } else {
            "nbench --nobuild --concurrent true --trace true --output $PerfResultsDir"
        }
        
        dotnet $nbenchArgs
        if ($LASTEXITCODE -ne 0) { throw "Performance tests failed for $($project.Name)" }
    }
}

function CreateNugetPackages {
    $projects = Get-ChildItem -Path "src" -Recurse -Filter "*.csproj" |
        Where-Object { $_.Name -notmatch ".*Tests.*\.csproj$" }

    foreach ($project in $projects) {
        $versionSuffixArg = if ($PreReleaseSuffix) { "--version-suffix $PreReleaseSuffix" } else { "" }
        dotnet pack $project.FullName -c $Configuration --no-build --include-symbols -o $NugetOutputDir $versionSuffixArg
        if ($LASTEXITCODE -ne 0) { throw "Package creation failed for $($project.Name)" }
    }
}

function SignPackages {
    if (-not $SignClientUser -or -not $SignClientSecret) {
        Write-Host "Skipping signing - no credentials provided"
        return
    }

    Get-ChildItem -Path $NugetOutputDir -Filter "*.nupkg" -Exclude "*.symbols.nupkg" | ForEach-Object {
        dotnet SignClient sign --config "$PSScriptRoot/appsettings.json" `
            -i $_.FullName `
            -r $SignClientUser `
            -s $SignClientSecret `
            -n "Incrementalist" `
            -d "Tool for generating incremental build data from Git diffs and Roslyn." `
            -u "https://github.com/petabridge/Incrementalist"
        if ($LASTEXITCODE -ne 0) { throw "Signing failed for $($_.Name)" }
    }
}

function PublishNugetPackages {
    if (-not $NugetApiKey -or -not $NugetSource) {
        Write-Host "Skipping NuGet publish - no API key or source provided"
        return
    }

    Get-ChildItem -Path $NugetOutputDir -Filter "*.nupkg" -Exclude "*.symbols.nupkg" | ForEach-Object {
        dotnet nuget push $_.FullName --api-key $NugetApiKey --source $NugetSource
        if ($LASTEXITCODE -ne 0) { throw "Package publish failed for $($_.Name)" }
    }
}



function ShowHelp {
    Write-Host @"
Usage: ./build.ps1 [target] [options]

Targets:
  * Build         - Builds the solution
  * Test          - Runs unit tests
  * IntegrationTest - Runs integration tests
  * NBench        - Runs performance tests
  * Nuget         - Creates NuGet packages
  * SignPackages  - Signs NuGet packages
  * PublishNuget  - Publishes NuGet packages
  * All           - Runs all targets
  * Help          - Shows this help

Options:
  -Configuration <value>     Build configuration (default: Release)
  -NugetApiKey <value>      NuGet API key for publishing
  -NugetSource <value>      NuGet source for publishing
  -SignClientUser <value>    Username for package signing
  -SignClientSecret <value>  Secret for package signing
  -PreReleaseTag <value>    Pre-release version suffix
"@
}

# Ensure tools are installed
EnsureTools

# Execute requested target
switch ($Target.ToLower()) {
    "clean" { Clean }
    "build" { 
        Clean
        UpdateAssemblyInfo
        Build
    }
    "test" { 
        Clean
        Build
        RunTests
    }
    "integrationtest" {
        Clean
        Build
        RunIntegrationTests
    }
    "nbench" {
        Clean
        Build
        RunNBenchTests
    }
    "nuget" {
        Clean
        UpdateAssemblyInfo
        Build
        RunTests
        CreateNugetPackages
    }
    "signpackages" {
        SignPackages
    }
    "publishnuget" {
        PublishNugetPackages
    }
    "all" {
        Clean
        UpdateAssemblyInfo
        Build
        RunTests
        RunIntegrationTests
        RunNBenchTests
        CreateNugetPackages
        SignPackages
        PublishNugetPackages

    }
    default { ShowHelp }
}