[CmdletBinding()]
param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release"
)

# Track overall success/failure and test counts
$script:hasUnexpectedFailures = $false
$script:totalTests = 0
$script:passedTests = 0
$script:failedTests = 0
$script:expectedFailures = 0

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
    
    $script:totalTests++
    Write-Host "`nRunning test: $TestName..." -ForegroundColor Cyan
    try {
        & $TestScript
        $exitCode = $LASTEXITCODE
        
        # Check for unexpected success or failure
        if ($exitCode -ne 0 -and -not $ExpectFailure) {
            Write-Host "[FAIL] Test failed unexpectedly: $TestName (Exit code: $exitCode)" -ForegroundColor Red
            $script:hasUnexpectedFailures = $true
            $script:failedTests++
        }
        elseif ($exitCode -eq 0 -and $ExpectFailure) {
            Write-Host "[FAIL] Test succeeded unexpectedly: $TestName (Expected failure)" -ForegroundColor Red
            $script:hasUnexpectedFailures = $true
            $script:failedTests++
        }
        else {
            if ($ExpectFailure -and $exitCode -ne 0) {
                Write-Host "[PASS] Test failed as expected: $TestName" -ForegroundColor Green
                $script:expectedFailures++
                $script:passedTests++
            } else {
                Write-Host "[PASS] Test completed successfully: $TestName" -ForegroundColor Green
                $script:passedTests++
            }
        }
    }
    catch {
        Write-Host "[FAIL] Test failed with exception: $TestName" -ForegroundColor Red
        Write-Host $_.Exception.Message -ForegroundColor Red
        $script:hasUnexpectedFailures = $true
        $script:failedTests++
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
Write-Host "Running Incrementalist integration tests..." -ForegroundColor Cyan
$testResultsDir = Initialize-TestEnvironment

$incrementalistProjects = Get-ChildItem -Path "src" -Filter "Incrementalist.Cmd.csproj" -Recurse
foreach ($project in $incrementalistProjects) {
    Write-Host "`nTesting project: $($project.FullName)" -ForegroundColor Cyan
    
    # Build project first
    Write-Host "Building project..."
    dotnet build $project.FullName -c $Configuration
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[FAIL] Failed to build Incrementalist.Cmd project" -ForegroundColor Red
        exit 1
    }
    
    # Run all test scenarios
    Test-FoldersOnly -ProjectPath $project.FullName -Configuration $Configuration -TestResultsDir $testResultsDir
    Test-SolutionCheck -ProjectPath $project.FullName -Configuration $Configuration -TestResultsDir $testResultsDir
    Test-CommandExecution -ProjectPath $project.FullName -Configuration $Configuration
    Test-ParallelExecution -ProjectPath $project.FullName -Configuration $Configuration
    Test-ErrorHandling -ProjectPath $project.FullName -Configuration $Configuration
}

# Final status report
Write-Host "`nIntegration Test Summary:" -ForegroundColor Cyan
Write-Host "Total Tests: $script:totalTests"
Write-Host "Passed     : $script:passedTests (including $script:expectedFailures expected failures)"
Write-Host "Failed     : $script:failedTests"

if ($script:hasUnexpectedFailures) {
    Write-Host "`n[FAIL] One or more integration tests failed unexpectedly!" -ForegroundColor Red
    exit 1
}
Write-Host "`n[PASS] All integration tests completed with expected results." -ForegroundColor Green
exit 0 