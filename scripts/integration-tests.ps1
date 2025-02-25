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

function Remove-IncrementalistCache {
    param(
        [Parameter(Mandatory=$true)]
        [string]$SolutionPath
    )
    
    $cacheDir = Join-Path (Split-Path $SolutionPath -Parent) ".incrementalist"
    if (Test-Path $cacheDir) {
        Write-Host "Removing existing cache directory: $cacheDir"
        Remove-Item -Path $cacheDir -Recurse -Force
    }
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
    Invoke-IncrementalistTest -TestName "Folders-only check (no cache)" -ProjectPath $ProjectPath -Configuration $Configuration -TestScript {
        dotnet run --project $ProjectPath -c $Configuration --no-build -- -b dev -l --no-cache -f $folderTestOutput
    }
}

function Test-SolutionCheck {
    param($ProjectPath, $Configuration, $TestResultsDir)
    
    $solutionTestOutput = Join-Path $TestResultsDir "incrementalist-affected-files.txt"
    Invoke-IncrementalistTest -TestName "Solution check (no cache)" -ProjectPath $ProjectPath -Configuration $Configuration -TestScript {
        dotnet run --project $ProjectPath -c $Configuration --no-build -- -b dev --no-cache -f $solutionTestOutput
    }
}

function Test-CommandExecution {
    param($ProjectPath, $Configuration)
    
    Invoke-IncrementalistTest -TestName "Command execution (no cache)" -ProjectPath $ProjectPath -Configuration $Configuration -TestScript {
        dotnet run --project $ProjectPath -c $Configuration --no-build -- -b dev -r --no-cache -- "build -c Release --nologo"
    }
}

function Test-ParallelExecution {
    param($ProjectPath, $Configuration)
    
    Invoke-IncrementalistTest -TestName "Parallel execution (no cache)" -ProjectPath $ProjectPath -Configuration $Configuration -TestScript {
        dotnet run --project $ProjectPath -c $Configuration --no-build -- -b dev -r --parallel --no-cache -- "build -c Release --nologo"
    }
}

function Test-ErrorHandling {
    param($ProjectPath, $Configuration)
    
    Invoke-IncrementalistTest -TestName "Error handling (no cache)" -ProjectPath $ProjectPath -Configuration $Configuration -TestScript {
        dotnet run --project $ProjectPath -c $Configuration --no-build -- -b dev -r --fail-on-no-projects --no-cache -- "invalid-command"
    } -ExpectFailure $true
}

function Test-CacheCreation {
    param($ProjectPath, $Configuration, $TestResultsDir)
    
    $solutionPath = Join-Path $PSScriptRoot "..\Incrementalist.sln"
    Remove-IncrementalistCache -SolutionPath $solutionPath
    
    $cacheTestOutput = Join-Path $TestResultsDir "incrementalist-cache-creation.txt"
    Invoke-IncrementalistTest -TestName "Cache creation" -ProjectPath $ProjectPath -Configuration $Configuration -TestScript {
        # Run without --no-cache to create the cache
        dotnet run --project $ProjectPath -c $Configuration --no-build -- -b dev -f $cacheTestOutput
    }
}

function Test-CacheReuse {
    param($ProjectPath, $Configuration, $TestResultsDir)
    
    $cacheTestOutput = Join-Path $TestResultsDir "incrementalist-cache-reuse.txt"
    Invoke-IncrementalistTest -TestName "Cache reuse" -ProjectPath $ProjectPath -Configuration $Configuration -TestScript {
        # Run again without --no-cache to reuse the existing cache
        dotnet run --project $ProjectPath -c $Configuration --no-build -- -b dev -f $cacheTestOutput
    }
}

function Test-ComplexCommandArguments {
    param($ProjectPath, $Configuration)
    
    # Create a test results directory with spaces to test path handling
    $testResultsDir = Join-Path $env:TEMP "Incrementalist Test Results"
    if (-not (Test-Path $testResultsDir)) {
        New-Item -ItemType Directory -Path $testResultsDir -Force | Out-Null
    }
    
    Invoke-IncrementalistTest -TestName "Complex command arguments" -ProjectPath $ProjectPath -Configuration $Configuration -TestScript {
        dotnet run --project $ProjectPath -c $Configuration --no-build -- -b dev -r --no-cache -- test `
            --logger "console;verbosity=detailed" `
            --collect:"XPlat Code Coverage;Format=cobertura" `
            --results-directory:"$testResultsDir" `
            --settings "$testResultsDir/test.runsettings" `
            /p:CollectCoverage=true `
            /p:CoverletOutputFormat=cobertura `
            /p:CoverletOutput="$testResultsDir/coverage.xml" `
            --blame-hang-timeout 5m
    }
    
    # Cleanup
    if (Test-Path $testResultsDir) {
        Remove-Item -Path $testResultsDir -Recurse -Force
    }
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
    Test-ComplexCommandArguments -ProjectPath $project.FullName -Configuration $Configuration
    
    # Run cache-specific tests
    Test-CacheCreation -ProjectPath $project.FullName -Configuration $Configuration -TestResultsDir $testResultsDir
    Test-CacheReuse -ProjectPath $project.FullName -Configuration $Configuration -TestResultsDir $testResultsDir
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