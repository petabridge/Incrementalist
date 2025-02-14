#!/usr/bin/env pwsh
param(
    [string] $Configuration = "Release",
    [string] $TestResultsPath = "TestResults"
)

$ErrorActionPreference = "Stop"

# Find the Incrementalist.Cmd project
$cmdProject = Get-ChildItem -Path "src" -Recurse -Filter "Incrementalist.Cmd.csproj" | Select-Object -First 1
if (-not $cmdProject) {
    throw "Could not find Incrementalist.Cmd.csproj"
}

# Create test results directory if it doesn't exist
if (-not (Test-Path $TestResultsPath)) {
    New-Item -ItemType Directory -Path $TestResultsPath | Out-Null
}

# Run tests for each framework
$frameworks = @("net6.0", "net7.0", "net8.0")
foreach ($framework in $frameworks) {
    Write-Host "Running integration tests for $framework"
    
    # Folders-only check
    Write-Host "Running folders-only check..."
    $folderArgs = "run --project `"$($cmdProject.FullName)`" -c $Configuration --framework $framework --no-build -- -b dev -l -f `"$TestResultsPath/incrementalist-affected-folders.txt`""
    Invoke-Expression "dotnet $folderArgs"
    if ($LASTEXITCODE -ne 0) { throw "Integration test (folders) failed for $framework" }

    # Solution check
    Write-Host "Running solution check..."
    $slnArgs = "run --project `"$($cmdProject.FullName)`" -c $Configuration --framework $framework --no-build -- -b dev -f `"$TestResultsPath/incrementalist-affected-files.txt`""
    Invoke-Expression "dotnet $slnArgs"
    if ($LASTEXITCODE -ne 0) { throw "Integration test (solution) failed for $framework" }
}