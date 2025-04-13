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
    $testResultsDir = Join-Path (Get-Location) "TestResults"
    if (-not (Test-Path $testResultsDir)) {
        New-Item -ItemType Directory -Path $testResultsDir -Force | Out-Null
    }
    return $testResultsDir
}

# Abstracts how we invoke the Incrementalist executable
function Run-Incrementalist {
    param(
        [Parameter(Mandatory=$false)]
        [string]$ProjectPath,

        [ValidateSet("Release", "Debug")]
        [Parameter(Mandatory=$false)]
        [string]$Configuration = "Release",

        [Parameter(Mandatory=$true)]
        [string[]]$IncrementalistArgs
    )

    # Run using dotnet run --project approach
    $cmd = "dotnet"
    $argList = @("run", "--project", $ProjectPath, "-c", $Configuration, "--no-build", "--")
    $argList += $IncrementalistArgs

    # Add delimiter and dotnet args if provided
    if ($DotNetArgs.Count -gt 0) {
        $argList += "--"
        $argList += $DotNetArgs
    }

    # Execute the command
    $process = Start-Process -FilePath $cmd -ArgumentList $argList -NoNewWindow -PassThru -Wait
    return $process.ExitCode
}

function Remove-IncrementalistCache {
    param(
        [Parameter(Mandatory=$true)]
        [string]$SolutionPath
    )
    
    $cacheDir = Join-Path (Split-Path $SolutionPath -Parent) ".incrementalist"
    if (Test-Path $cacheDir) {
        Write-Host "Removing existing cache directory: $cacheDir"
        Remove-Item -Path $cacheDir -Recurse -Force -ErrorAction SilentlyContinue
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
        # Explicitly capture the return value from the script block
        $exitCode = & $TestScript
        
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
        Run-Incrementalist -ProjectPath $ProjectPath -Configuration $Configuration -IncrementalistArgs @("-b", "dev", "-l", "--no-cache", "-f" , $folderTestOutput)
    }
}

function Test-SolutionCheck {
    param($ProjectPath, $Configuration, $TestResultsDir)
    
    $solutionTestOutput = Join-Path $TestResultsDir "incrementalist-affected-files.txt"
    Invoke-IncrementalistTest -TestName "Solution check (no cache)" -ProjectPath $ProjectPath -Configuration $Configuration -TestScript {
        Run-Incrementalist -ProjectPath $ProjectPath -Configuration $Configuration -IncrementalistArgs @("-b", "dev", "--no-cache", "-f", $solutionTestOutput)
    }
}

function Test-CommandExecution {
    param($ProjectPath, $Configuration)
    
    Invoke-IncrementalistTest -TestName "Command execution (no cache)" -ProjectPath $ProjectPath -Configuration $Configuration -TestScript {
        Run-Incrementalist -ProjectPath $ProjectPath -Configuration $Configuration -IncrementalistArgs @("-b", "dev", "-r", "--no-cache", "--", "build", "-c", "Release", "--nologo")
    }
}

function Test-ParallelExecution {
    param($ProjectPath, $Configuration)
    
    Invoke-IncrementalistTest -TestName "Parallel execution (no cache)" -ProjectPath $ProjectPath -Configuration $Configuration -TestScript {
        Run-Incrementalist -ProjectPath $ProjectPath -Configuration $Configuration -IncrementalistArgs @("-b", "dev", "-r", "--parallel", "--no-cache", "--", "build", "-c", "Release", "--nologo")
    }
}

function Test-ErrorHandling {
    param($ProjectPath, $Configuration)
    
    Invoke-IncrementalistTest -TestName "Error handling (no cache)" -ProjectPath $ProjectPath -Configuration $Configuration -TestScript {
        Run-Incrementalist -ProjectPath $ProjectPath -Configuration $Configuration -IncrementalistArgs @("-b", "dev", "-r", "--fail-on-no-projects", "--no-cache", "--", "invalid-command")
    } -ExpectFailure $true
}

function Test-CacheCreation {
    param($ProjectPath, $Configuration, $TestResultsDir)
    
    $solutionPath = Join-Path $PSScriptRoot "..\Incrementalist.sln"
    Remove-IncrementalistCache -SolutionPath $solutionPath
    
    $cacheTestOutput = Join-Path $TestResultsDir "incrementalist-cache-creation.txt"
    Invoke-IncrementalistTest -TestName "Cache creation" -ProjectPath $ProjectPath -Configuration $Configuration -TestScript {
        # Run without --no-cache to create the cache
        Run-Incrementalist -ProjectPath $ProjectPath -Configuration $Configuration -IncrementalistArgs @("-b", "dev", "--no-cache", "-f", $cacheTestOutput)
    }
}

function Test-CacheReuse {
    param($ProjectPath, $Configuration, $TestResultsDir)
    
    $cacheTestOutput = Join-Path $TestResultsDir "incrementalist-cache-reuse.txt"
    Invoke-IncrementalistTest -TestName "Cache reuse" -ProjectPath $ProjectPath -Configuration $Configuration -TestScript {
        # Run again without --no-cache to reuse the existing cache
        Run-Incrementalist -ProjectPath $ProjectPath -Configuration $Configuration -IncrementalistArgs @("-b", "dev", "--no-cache", "-f", $cacheTestOutput)
        Run-Incrementalist -ProjectPath $ProjectPath -Configuration $Configuration -IncrementalistArgs @("-b", "dev", "-f", $cacheTestOutput)
    }
}

function Test-ComplexCommandArguments {
    param($ProjectPath, $Configuration)
    
    # Create a test results directory with spaces to test path handling
    $testResultsDir = Join-Path ([System.IO.Path]::GetTempPath()) "Incrementalist Test Results"
    if (-not (Test-Path $testResultsDir)) {
        New-Item -ItemType Directory -Path $testResultsDir -Force | Out-Null
    }
    
    Invoke-IncrementalistTest -TestName "Complex command arguments" -ProjectPath $ProjectPath -Configuration $Configuration -TestScript {

        $incrementalistArgs = @(
            "-b", "dev",
            "-r",
            "--no-cache",
            "--",  # Separator for dotnet command arguments
            "test",
            "--logger", "console;verbosity=detailed",
            "--collect:`"XPlat Code Coverage`"", # Need to escape quotes inside the string
            "--results-directory:`"$testResultsDir`"", # Need to escape quotes inside the string
            "/p:CollectCoverage=true",
            "/p:CoverletOutputFormat=cobertura",
            "/p:CoverletOutput=`"$testResultsDir/coverage.xml`"", # Need to escape quotes inside the string
            "--blame-hang-timeout", "5m"
        )
        
        Run-Incrementalist -ProjectPath $ProjectPath -Configuration $Configuration -IncrementalistArgs $incrementalistArgs
    }
    
    # Cleanup
    if (Test-Path $testResultsDir) {
        Remove-Item -Path $testResultsDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# Reproduction for https://github.com/petabridge/Incrementalist/issues/378
function Test-SimilarDotnetArguments {
    param($ProjectPath, $Configuration)
    # Create a test results directory with spaces to test path handling
    $testResultsDir = Join-Path ([System.IO.Path]::GetTempPath()) "Incrementalist Test Results"
    if (-not (Test-Path $testResultsDir)) {
        New-Item -ItemType Directory -Path $testResultsDir -Force | Out-Null
    }

    Invoke-IncrementalistTest -TestName "Similar Incrementalist and dotnet Arguments" -ProjectPath $ProjectPath -Configuration $Configuration -TestScript {
        Run-Incrementalist -ProjectPath $ProjectPath -Configuration $Configuration -IncrementalistArgs @("-b", "dev", "-c", "-r", "--no-cache")
    }

    # Cleanup
    if (Test-Path $testResultsDir) {
        Remove-Item -Path $testResultsDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# Test targeting with glob patterns
function Test-GlobTargeting {
    param($ProjectPath, $Configuration, $TestResultsDir)
    
    $baselineOutput = Join-Path $TestResultsDir "incrementalist-target-baseline.txt"
    $targetGlobOutput = Join-Path $TestResultsDir "incrementalist-target-glob.txt"
    Invoke-IncrementalistTest -TestName "Glob targeting" -ProjectPath $ProjectPath -Configuration $Configuration -TestScript {
        # First, run a baseline to check if any changes are detected
        Write-Host "Running baseline to check for changes..."
        $exitCodeBaseline = Run-Incrementalist -ProjectPath $ProjectPath -Configuration $Configuration -IncrementalistArgs @("-b", "dev", "--no-cache", "-f", $baselineOutput)
        if ($exitCodeBaseline -ne 0) { throw "Incrementalist baseline command failed with exit code $exitCodeBaseline" }
        
        $baselineProjects = @(Get-Content $baselineOutput -ErrorAction SilentlyContinue)
        $changeDetected = ($baselineProjects | Measure-Object).Count -gt 0
        
        if (-not $changeDetected) {
            Write-Host "No changes detected between current branch and target branch (dev). Skipping verification." -ForegroundColor Yellow
            return 0 # Skip the rest of the test if no changes detected
        }

        # Construct the expected path more explicitly
        $parentDir = Split-Path -Path $PSScriptRoot -Parent
        $expectedProjectPath = Join-Path -Path $parentDir -ChildPath "src\Incrementalist\Incrementalist.csproj"
        $expectedProjectFullPath = (Resolve-Path -Path $expectedProjectPath -ErrorAction Stop).Path
        
        # Check if the expected project is in the baseline changes
        $expectedProjectInChanges = $false
        foreach ($project in $baselineProjects) {
            if ($project.Trim() -eq $expectedProjectFullPath.Trim()) {
                $expectedProjectInChanges = $true
                break
            }
        }
        
        if (-not $expectedProjectInChanges) {
            Write-Host "Expected project ($expectedProjectFullPath) not found in detected changes. Skipping verification." -ForegroundColor Yellow
            Write-Host "Detected changes:" -ForegroundColor Yellow
            $baselineProjects | ForEach-Object { Write-Host " - $_" -ForegroundColor Yellow }
            return 0 # Skip the rest of the test if the expected project isn't in the changes
        }

        # Run Incrementalist with target glob
        #dotnet run --project $ProjectPath -c $Configuration --no-build -- -b dev --target-glob "**/Incrementalist.csproj" --no-cache -f $targetGlobOutput
        $exitCodeTarget = Run-Incrementalist -ProjectPath $ProjectPath -Configuration $Configuration -IncrementalistArgs @("-b", "dev", "--target-glob", "**/Incrementalist.csproj", "--no-cache", "-f", $targetGlobOutput)
        if ($exitCodeTarget -ne 0) { throw "Incrementalist command failed with exit code $exitCodeTarget" }

        # Verification logic
        $actualProjects = @(Get-Content $targetGlobOutput -ErrorAction SilentlyContinue) # Ensure it's always an array
        
        # Check if the file contains exactly one line matching the expected project string, ignoring whitespace
        if (($actualProjects | Measure-Object).Count -ne 1 -or `
            -not ($actualProjects[0].Trim() -eq $expectedProjectFullPath.Trim()) ) { # Trim() works on string now
            Write-Host "Expected output:`n$expectedProjectFullPath`nActual output:`n$($actualProjects -join "`n")" -ForegroundColor Yellow
            throw "Glob targeting verification failed. Output file content did not match expected project."
        }
    }
}

# Test skipping with glob patterns
function Test-GlobSkipping {
    param($ProjectPath, $Configuration, $TestResultsDir)
    
    $baselineOutput = Join-Path $TestResultsDir "incrementalist-skip-baseline.txt"
    $skipGlobOutput = Join-Path $TestResultsDir "incrementalist-skip-glob.txt"
    Invoke-IncrementalistTest -TestName "Glob skipping" -ProjectPath $ProjectPath -Configuration $Configuration -TestScript {
        # 1. Run without skip to get baseline affected projects
        Write-Host "Running baseline to determine affected projects..."
        $exitCodeBaseline = Run-Incrementalist -ProjectPath $ProjectPath -Configuration $Configuration -IncrementalistArgs @("-b", "dev", "--no-cache", "-f", $baselineOutput)
        if ($exitCodeBaseline -ne 0) { throw "Incrementalist command (baseline) failed with exit code $exitCodeBaseline" }
        
        $baselineProjects = @(Get-Content $baselineOutput -ErrorAction SilentlyContinue | ForEach-Object { (Resolve-Path $_).Path }) | Sort-Object
        Write-Host "Baseline projects count: $($baselineProjects.Count)"
        
        # Check if any changes were detected
        if ($baselineProjects.Count -eq 0) {
            Write-Host "No changes detected between current branch and target branch (dev). Skipping verification." -ForegroundColor Yellow
            return 0 # Skip the rest of the test
        }
        
        # Check if any Test projects are in the baseline changes
        $testProjectsInChanges = $baselineProjects | Where-Object { $_ -like "*.Tests.csproj" }
        $hasTestProjects = ($testProjectsInChanges | Measure-Object).Count -gt 0
        
        # Detect non-test projects
        $nonTestProjects = $baselineProjects | Where-Object { $_ -notlike "*.Tests.csproj" }
        $hasNonTestProjects = ($nonTestProjects | Measure-Object).Count -gt 0
        
        # If there are no test projects or no non-test projects, we can't properly verify skipping behavior
        if (-not $hasTestProjects) {
            Write-Host "No test projects (*.Tests.csproj) found in detected changes. Skipping verification as the skip-glob pattern wouldn't affect results." -ForegroundColor Yellow
            Write-Host "Detected changes:" -ForegroundColor Yellow
            $baselineProjects | ForEach-Object { Write-Host " - $_" -ForegroundColor Yellow }
            return 0 # Skip the rest of the test
        }
        
        if (-not $hasNonTestProjects) {
            Write-Host "Only test projects found in detected changes. Skipping verification as there would be no projects after skipping." -ForegroundColor Yellow
            Write-Host "Detected changes:" -ForegroundColor Yellow
            $baselineProjects | ForEach-Object { Write-Host " - $_" -ForegroundColor Yellow }
            return 0 # Skip the rest of the test
        }

        # 2. Run with skip glob
        Write-Host "Running with skip glob..."
        $exitCodeSkip = Run-Incrementalist -ProjectPath $ProjectPath -Configuration $Configuration -IncrementalistArgs @("-b", "dev", "--skip-glob", "**/*.Tests.csproj", "--no-cache", "-f", $skipGlobOutput)
        if ($exitCodeSkip -ne 0) { throw "Incrementalist command (skip glob) failed with exit code $exitCodeSkip" }
        $skippedProjects = (Get-Content $skipGlobOutput -ErrorAction SilentlyContinue | ForEach-Object { (Resolve-Path $_).Path }) | Sort-Object
        Write-Host "Skipped projects count: $($skippedProjects.Count)"
        
        # 3. Verification logic
        $expectedSkippedProjects = $baselineProjects | Where-Object { $_ -notlike "*.Tests.csproj" }
        Write-Host "Expected skipped projects count: $($expectedSkippedProjects.Count)"
        
        # Compare the actual list after skipping with the expected list
        $diff = Compare-Object -ReferenceObject $expectedSkippedProjects -DifferenceObject $skippedProjects -IncludeEqual
        $mismatched = $diff | Where-Object { $_.SideIndicator -ne "==" }
        
        if ($mismatched.Count -ne 0) {
            Write-Host "Verification failed. Expected projects after skipping did not match actual." -ForegroundColor Yellow
            Write-Host "--- Expected Projects ($($expectedSkippedProjects.Count)) ---" -ForegroundColor Yellow
            $expectedSkippedProjects | Write-Host -ForegroundColor Yellow
            Write-Host "--- Actual Projects ($($skippedProjects.Count)) ---" -ForegroundColor Yellow
            $skippedProjects | Write-Host -ForegroundColor Yellow
            Write-Host "--- Differences ---" -ForegroundColor Yellow
            $mismatched | Format-Table -AutoSize | Out-String | Write-Host -ForegroundColor Yellow
            throw "Glob skipping verification failed."
        }
        
        # Return success if all verifications passed
        return 0
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
    Test-SimilarDotnetArguments -ProjectPath $project.FullName -Configuration $Configuration
    
    # Run cache-specific tests
    Test-CacheCreation -ProjectPath $project.FullName -Configuration $Configuration -TestResultsDir $testResultsDir
    Test-CacheReuse -ProjectPath $project.FullName -Configuration $Configuration -TestResultsDir $testResultsDir
    
    # Run glob tests
    Test-GlobTargeting -ProjectPath $project.FullName -Configuration $Configuration -TestResultsDir $testResultsDir
    Test-GlobSkipping -ProjectPath $project.FullName -Configuration $Configuration -TestResultsDir $testResultsDir
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