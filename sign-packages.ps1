#!/usr/bin/env pwsh
param(
    [Parameter(Mandatory=$true)]
    [string] $SignClientUser,
    
    [Parameter(Mandatory=$true)]
    [string] $SignClientSecret,
    
    [Parameter(Mandatory=$true)]
    [string] $PackagesPath
)

$ErrorActionPreference = "Stop"

# Install SignClient if not already installed
$signClientPath = Get-Command "SignClient.exe" -ErrorAction SilentlyContinue
if (-not $signClientPath) {
    Write-Host "SignClient.exe not found. Installing..."
    dotnet tool install --global SignClient
    if ($LASTEXITCODE -ne 0) { throw "Failed to install SignClient" }
}

# Sign each NuGet package
Get-ChildItem -Path $PackagesPath -Filter "*.nupkg" -Exclude "*.symbols.nupkg" | ForEach-Object {
    Write-Host "Signing package: $($_.Name)"
    SignClient sign --config "$PSScriptRoot/appsettings.json" `
        -i $_.FullName `
        -r $SignClientUser `
        -s $SignClientSecret `
        -n "Incrementalist" `
        -d "Tool for generating incremental build data from Git diffs and Roslyn." `
        -u "https://github.com/petabridge/Incrementalist"
    if ($LASTEXITCODE -ne 0) { throw "Signing failed for $($_.Name)" }
}