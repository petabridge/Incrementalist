[CmdletBinding()]
param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = 'Stop'

Write-Host "Running Incrementalist integration tests..."
$incrementalistProjects = Get-ChildItem -Path "src" -Filter "Incrementalist.Cmd.csproj" -Recurse

foreach ($project in $incrementalistProjects) {
    Write-Host "Running integration tests for $($project.FullName)"
    
    # Build the project first
    dotnet build $project.FullName -c $Configuration
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to build Incrementalist.Cmd project"
    }

    # Test 1: Folders-only check
    Write-Host "Running Incrementalist folders-only check..."
    $folderTestOutput = Join-Path $pwd "TestResults/incrementalist-affected-folders.txt"
    dotnet run --project $project.FullName -c $Configuration --no-build -- -b dev -l -f $folderTestOutput
    if ($LASTEXITCODE -ne 0) {
        throw "Incrementalist folders-only check failed"
    }

    # Test 2: Solution check
    Write-Host "Running Incrementalist solution check..."
    $solutionTestOutput = Join-Path $pwd "TestResults/incrementalist-affected-files.txt"
    dotnet run --project $project.FullName -c $Configuration --no-build -- -b dev -f $solutionTestOutput
    if ($LASTEXITCODE -ne 0) {
        throw "Incrementalist solution check failed"
    }

    # Test 3: Command execution
    Write-Host "Running Incrementalist with build command..."
    dotnet run --project $project.FullName -c $Configuration --no-build -- -b dev -r -- "build -c Release --nologo"
    if ($LASTEXITCODE -ne 0) {
        throw "Incrementalist command execution failed"
    }

    # Test 4: Parallel execution
    Write-Host "Running Incrementalist with parallel build command..."
    dotnet run --project $project.FullName -c $Configuration --no-build -- -b dev -r --parallel -- "build -c Release --nologo"
    if ($LASTEXITCODE -ne 0) {
        throw "Incrementalist parallel command execution failed"
    }

    # Test 5: Error handling
    Write-Host "Testing Incrementalist error handling..."
    dotnet run --project $project.FullName -c $Configuration --no-build -- -b dev -r --fail-on-no-projects -- "invalid-command"
    if ($LASTEXITCODE -eq 0) {
        throw "Expected Incrementalist to fail with invalid command"
    }
}

Write-Host "Integration tests completed successfully!" 