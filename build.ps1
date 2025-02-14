[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string] $Target = "Help",
    [string] $Configuration = "Release",
    [switch] $NoTests,
    [string] $VersionSuffix = "",
    [string] $NugetApiKey = "",
    [string] $NugetSource = "",
    [string] $SignClientSecret = "",
    [string] $SignClientUser = ""
)

$ErrorActionPreference = "Stop"

$ArtifactsDir = Join-Path $PSScriptRoot "bin"
$NugetDir = Join-Path $ArtifactsDir "nuget"
$TestResultsDir = Join-Path $PSScriptRoot "TestResults"
$PerfResultsDir = Join-Path $PSScriptRoot "PerfResults"
$DocSiteDir = Join-Path $PSScriptRoot "docs/_site"
$BuildNumber = if ($env:BUILD_NUMBER) { $env:BUILD_NUMBER } else { "0" }

# Read release notes
$ReleaseNotes = Get-Content -Path (Join-Path $PSScriptRoot "RELEASE_NOTES.md") | Select-Object -First 1
$ReleaseVersion = $ReleaseNotes -replace "#### ","" # Assumes release notes format like "#### 0.1.0"

function Clean-Artifacts {
    if (Test-Path $ArtifactsDir) { Remove-Item -Recurse -Force $ArtifactsDir }
    if (Test-Path $TestResultsDir) { Remove-Item -Recurse -Force $TestResultsDir }
    if (Test-Path $PerfResultsDir) { Remove-Item -Recurse -Force $PerfResultsDir }
    if (Test-Path $DocSiteDir) { Remove-Item -Recurse -Force $DocSiteDir }
}

function Update-ProjectVersion {
    $propsFile = Join-Path $PSScriptRoot "src/Directory.Build.props"
    $xml = [xml](Get-Content $propsFile)
    $versionNode = $xml.SelectSingleNode("//Project/PropertyGroup/VersionPrefix")
    $versionNode.InnerText = $ReleaseVersion
    $releaseNotesNode = $xml.SelectSingleNode("//Project/PropertyGroup/PackageReleaseNotes")
    $releaseNotesNode.InnerText = (Get-Content -Path (Join-Path $PSScriptRoot "RELEASE_NOTES.md") | Select-Object -Skip 1) -join "`n"
    $xml.Save($propsFile)
}

function Build-Solution {
    dotnet restore
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    
    dotnet build -c $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

function Run-Tests {
    $testProjects = Get-ChildItem -Path "src" -Filter "*.Tests.csproj" -Recurse
    foreach ($project in $testProjects) {
        $loggerArgs = if ($env:BUILD_NUMBER) {
            "--logger:trx --logger:""console;verbosity=normal"" --results-directory $TestResultsDir -- -parallel none -teamcity"
        } else {
            "--logger:trx --logger:""console;verbosity=normal"" --results-directory $TestResultsDir -- -parallel none"
        }
        
        dotnet test $project.FullName -c $Configuration --no-build $loggerArgs
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
}

function Run-IntegrationTests {
    $frameworks = @("net6.0", "net7.0", "net8.0")
    $cmdProject = Get-ChildItem -Path "src" -Filter "Incrementalist.Cmd.csproj" -Recurse | Select-Object -First 1
    
    foreach ($framework in $frameworks) {
        # Folders-only check
        dotnet run --project $cmdProject.FullName -c $Configuration --framework $framework --no-build -- -b dev -l -f "$TestResultsDir/incrementalist-affected-folders.txt"
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        
        # Solution check
        dotnet run --project $cmdProject.FullName -c $Configuration --framework $framework --no-build -- -b dev -f "$TestResultsDir/incrementalist-affected-files.txt"
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
}

function Create-NuGetPackages {
    if (!(Test-Path $NugetDir)) { New-Item -ItemType Directory -Path $NugetDir }
    
    $projects = Get-ChildItem -Path "src" -Filter "*.csproj" -Recurse | 
                Where-Object { $_.Name -notmatch "Tests" }
                
    foreach ($project in $projects) {
        $versionSuffixArg = if ($VersionSuffix) { "--version-suffix $VersionSuffix" } else { "" }
        dotnet pack $project.FullName -c $Configuration --no-build --include-symbols -o $NugetDir $versionSuffixArg
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
}

function Sign-NuGetPackages {
    if ($SignClientSecret -and $SignClientUser) {
        $packages = Get-ChildItem -Path $NugetDir -Filter "*.nupkg" | Where-Object { !$_.Name.Contains(".symbols.") }
        $signClientPath = Get-Command SignClient -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source
        
        if (!$signClientPath) {
            Write-Error "SignClient tool not found. Please install it using: dotnet tool install -g SignClient"
            exit 1
        }
        
        foreach ($package in $packages) {
            & $signClientPath sign --config (Join-Path $PSScriptRoot "appsettings.json") `
                                 -i $package.FullName `
                                 -r $SignClientUser `
                                 -s $SignClientSecret `
                                 -n "Incrementalist" `
                                 -d "Tool for generating incremental build data from Git diffs and Roslyn." `
                                 -u "https://github.com/petabridge/Incrementalist"
            if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        }
    }
}

function Publish-NuGetPackages {
    if ($NugetApiKey -and $NugetSource) {
        $packages = Get-ChildItem -Path $NugetDir -Filter "*.nupkg" | Where-Object { !$_.Name.Contains(".symbols.") }
        foreach ($package in $packages) {
            dotnet nuget push $package.FullName --api-key $NugetApiKey --source $NugetSource
            if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        }
    }
}

function Build-Documentation {
    dotnet restore
    dotnet build -c $Configuration --no-restore
    
    $docfxPath = Join-Path $PSScriptRoot "docs/docfx.json"
    & docfx $docfxPath
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

function Show-Help {
    Write-Host @"
Usage: ./build.ps1 [target] [options]

Targets:
 * Build         Builds the solution
 * Test          Runs all tests
 * IntegrationTest Runs integration tests
 * Nuget         Creates NuGet packages
 * SignPackages  Signs NuGet packages (requires SignClientSecret and SignClientUser)
 * Publish       Publishes NuGet packages (requires NugetApiKey and NugetSource)
 * Doc           Builds documentation
 * All           Runs all targets (build, test, nuget, sign, publish)
 * Help          Shows this help

Options:
 -Configuration     Build configuration (default: Release)
 -NoTests          Skip running tests
 -VersionSuffix    Version suffix for NuGet packages
 -NugetApiKey      API key for NuGet publishing
 -NugetSource      NuGet source for publishing
 -SignClientSecret Secret for package signing
 -SignClientUser   User for package signing
"@
}

# Execute requested target
switch ($Target.ToLower()) {
    "clean" {
        Clean-Artifacts
    }
    "build" {
        Clean-Artifacts
        Update-ProjectVersion
        Build-Solution
    }
    "test" {
        if (!$NoTests) {
            Build-Solution
            Run-Tests
        }
    }
    "integrationtest" {
        Build-Solution
        Run-IntegrationTests
    }
    "nuget" {
        Build-Solution
        Create-NuGetPackages
    }
    "signpackages" {
        Sign-NuGetPackages
    }
    "publish" {
        Publish-NuGetPackages
    }
    "doc" {
        Build-Documentation
    }
    "all" {
        Clean-Artifacts
        Update-ProjectVersion
        Build-Solution
        if (!$NoTests) {
            Run-Tests
            Run-IntegrationTests
        }
        Create-NuGetPackages
        Sign-NuGetPackages
        Publish-NuGetPackages
        Build-Documentation
    }
    default {
        Show-Help
    }
}

# Cleanup
dotnet build-server shutdown