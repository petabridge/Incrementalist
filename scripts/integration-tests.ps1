[CmdletBinding()]
param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = 'Stop'

function Initialize-TestEnvironment {
    $testResultsDir = Join-Path $PSScriptRoot "..\TestResults"
    if (-not (Test-Path $testResultsDir)) {
        New-Item -ItemType Directory -Path $testResultsDir -Force | Out-Null
    }
    return $testResultsDir
}

function Invoke-IncrementalistTest {
    param(
        [Parameter(Mandatory=$true)]
        [string]$TestName,
        
        [Parameter(Mandatory=$true)]
        [string]$ProjectPath,
        
        [Parameter(Mandatory=$true)]
        [string]$Configuration,
        
        [Parameter(Mandatory=$true)]
        [scriptblock]$TestScript,
        
        [Parameter(Mandatory=$false)]
        [bool]$ExpectFailure = $false
    )
    
    Write-Host "Running test: $TestName..."
    try {
        & $TestScript
        if ($LASTEXITCODE -ne 0 -and -not $ExpectFailure) {
            throw "Test failed with exit code $LASTEXITCODE"
        }
        if ($LASTEXITCODE -eq 0 -and $ExpectFailure) {
            throw "Test was expected to fail but succeeded"
        }
        Write-Host "Test completed successfully: $TestName" -ForegroundColor Green
    }
    catch {
        Write-Host "Test failed: $TestName" -ForegroundColor Red
        Write-Host $_.Exception.Message
        throw
    }
}

function Test-FoldersOnly {
    param($ProjectPath, $Configuration, $TestResultsDir)
    
    $folderTestOutput = Join-Path $TestResultsDir "incrementalist-affected-folders.txt"
    Invoke-IncrementalistTest -TestName "Folders-only check" -ProjectPath $ProjectPath -Configuration $Configuration -TestScript {
        dotnet run --project $ProjectPath -c $Configuration --no-build -- -b dev -l -f $folderTestOutput
    }
}

function Test-SolutionCheck {
    param($ProjectPath, $Configuration, $TestResultsDir)
    
    $solutionTestOutput = Join-Path $TestResultsDir "incrementalist-affected-files.txt"
    Invoke-IncrementalistTest -TestName "Solution check" -ProjectPath $ProjectPath -Configuration $Configuration -TestScript {
        dotnet run --project $ProjectPath -c $Configuration --no-build -- -b dev -f $solutionTestOutput
    }
}

function Test-CommandExecution {
    param($ProjectPath, $Configuration)
    
    Invoke-IncrementalistTest -TestName "Command execution" -ProjectPath $ProjectPath -Configuration $Configuration -TestScript {
        dotnet run --project $ProjectPath -c $Configuration --no-build -- -b dev -r -- "build -c Release --nologo"
    }
}

function Test-ParallelExecution {
    param($ProjectPath, $Configuration)
    
    Invoke-IncrementalistTest -TestName "Parallel execution" -ProjectPath $ProjectPath -Configuration $Configuration -TestScript {
        dotnet run --project $ProjectPath -c $Configuration --no-build -- -b dev -r --parallel -- "build -c Release --nologo"
    }
}

function Test-ErrorHandling {
    param($ProjectPath, $Configuration)
    
    Invoke-IncrementalistTest -TestName "Error handling" -ProjectPath $ProjectPath -Configuration $Configuration -TestScript {
        dotnet run --project $ProjectPath -c $Configuration --no-build -- -b dev -r --fail-on-no-projects -- "invalid-command"
    } -ExpectFailure $true
}

# Main execution
Write-Host "Running Incrementalist integration tests..."
$testResultsDir = Initialize-TestEnvironment

$incrementalistProjects = Get-ChildItem -Path "src" -Filter "Incrementalist.Cmd.csproj" -Recurse
foreach ($project in $incrementalistProjects) {
    Write-Host "Testing project: $($project.FullName)" -ForegroundColor Cyan
    
    # Build project first
    Write-Host "Building project..."
    dotnet build $project.FullName -c $Configuration
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to build Incrementalist.Cmd project"
    }
    
    # Run all test scenarios
    Test-FoldersOnly -ProjectPath $project.FullName -Configuration $Configuration -TestResultsDir $testResultsDir
    Test-SolutionCheck -ProjectPath $project.FullName -Configuration $Configuration -TestResultsDir $testResultsDir
    Test-CommandExecution -ProjectPath $project.FullName -Configuration $Configuration
    Test-ParallelExecution -ProjectPath $project.FullName -Configuration $Configuration
    Test-ErrorHandling -ProjectPath $project.FullName -Configuration $Configuration
}

Write-Host "All integration tests completed successfully!" -ForegroundColor Green 