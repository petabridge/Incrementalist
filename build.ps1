[CmdletBinding()]
Param(
    [string]$Target = "Build",
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",
    [switch]$NoTest,
    [switch]$NoIntegrationTest,
    [switch]$NoPack,
    [switch]$NoSign
)

# Read release notes and version
$releaseNotesPath = "./RELEASE_NOTES.md"
$releaseNotes = Get-Content $releaseNotesPath -Raw
if ($releaseNotes -match '#### (\d+\.\d+\.\d+)') {
    $version = $Matches[1]
} else {
    throw "Version not found in RELEASE_NOTES.md"
}

# Clean
Write-Host "Cleaning..." -ForegroundColor Green
dotnet clean -c $Configuration
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue ./bin
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue ./TestResults
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue ./PerfResults

# Restore tools
Write-Host "Restoring .NET tools..." -ForegroundColor Green
dotnet tool restore

# Build
Write-Host "Building..." -ForegroundColor Green
dotnet build -c $Configuration

# Tests
if (-not $NoTest) {
    Write-Host "Running tests..." -ForegroundColor Green
    dotnet test -c $Configuration --no-build --logger:trx --logger:"console;verbosity=normal" --results-directory ./TestResults
}

# Integration Tests
if (-not $NoIntegrationTest) {
    Write-Host "Running integration tests..." -ForegroundColor Green
    $frameworks = @("net6.0", "net7.0", "net8.0")
    foreach ($framework in $frameworks) {
        Write-Host "Testing framework $framework..." -ForegroundColor Yellow
        # Folders-only check
        dotnet run --project ./src/Incrementalist.Cmd/Incrementalist.Cmd.csproj -c $Configuration --framework $framework --no-build -- -b dev -l -f ./TestResults/incrementalist-affected-folders.txt
        # Solution check
        dotnet run --project ./src/Incrementalist.Cmd/Incrementalist.Cmd.csproj -c $Configuration --framework $framework --no-build -- -b dev -f ./TestResults/incrementalist-affected-files.txt
    }
}

# Pack
if (-not $NoPack) {
    Write-Host "Creating NuGet packages..." -ForegroundColor Green
    $projects = Get-ChildItem -Path ./src -Filter *.csproj -Recurse | 
                Where-Object { $_.Name -notmatch "Tests" }
    
    foreach ($project in $projects) {
        dotnet pack $project.FullName -c $Configuration --no-build --include-symbols -o ./bin/nuget
    }
}

# Sign
if (-not $NoSign -and (Test-Path env:SignClientSecret) -and (Test-Path env:SignClientUser)) {
    Write-Host "Signing NuGet packages..." -ForegroundColor Green
    $packages = Get-ChildItem ./bin/nuget/*.nupkg -Exclude *.symbols.nupkg
    
    foreach ($package in $packages) {
        dotnet signclient sign `
            --config ./appsettings.json `
            --input $package.FullName `
            --user $env:SignClientUser `
            --secret $env:SignClientSecret `
            --name "Incrementalist" `
            --description "Tool for generating incremental build data from Git diffs and Roslyn." `
            --url "https://github.com/petabridge/Incrementalist"
    }
}

Write-Host "Build completed successfully!" -ForegroundColor Green