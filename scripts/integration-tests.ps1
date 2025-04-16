[CmdletBinding()]
param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",

    [Parameter()]
    [ValidateSet("Project", "Tool")]
    [string]$ExecutionMode = "Tool",

    [Parameter(Mandatory=$false)]
    [bool]$VerboseLogging = $false
)

# Source helper scripts
. "$PSScriptRoot/getReleaseNotes.ps1"

# Track overall success/failure and test counts
$script:hasUnexpectedFailures = $false
$script:totalTests = 0
$script:passedTests = 0
$script:failedTests = 0
$script:expectedFailures = 0


# Global variables for tool installation if needed
$script:toolPath = $null
$script:toolInstalled = $false

function Initialize-TestEnvironment {
    $testResultsDir = Join-Path (Get-Location) "TestResults"
    if (-not (Test-Path $testResultsDir)) {
        New-Item -ItemType Directory -Path $testResultsDir -Force | Out-Null
    }
    return $testResultsDir
}

# Helper function to install Incrementalist as a tool
function Install-IncrementalistTool {
    param(
        [Parameter(Mandatory=$true)]
        [string]$ProjectPath,
        
        [Parameter(Mandatory=$true)]
        [string]$Configuration
    )
    
    # Get base version from release notes
    $changelogPath = Join-Path $PSScriptRoot "..\RELEASE_NOTES.md"
    if (-not (Test-Path $changelogPath)) {
        throw "RELEASE_NOTES.md not found at $changelogPath"
    }
    $releaseInfo = Get-ReleaseNotes -MarkdownFile $changelogPath
    $baseVersion = $releaseInfo.Version
    if (-not $baseVersion) {
        throw "Could not determine base version from $changelogPath"
    }
    
    # Create unique suffix and full version
    $suffix = "ci-$([DateTime]::UtcNow.Ticks)"
    $fullVersion = "$($baseVersion)-$($suffix)"
    Write-Host "Using Version: $fullVersion (Base: $baseVersion, Suffix: $suffix)" -ForegroundColor Yellow

    # Create temporary directory for packaging
    $packageOutput = Join-Path ([System.IO.Path]::GetTempPath()) ([System.Guid]::NewGuid().ToString("N"))
    # $installPath = Join-Path ([System.IO.Path]::GetTempPath()) ([System.Guid]::NewGuid().ToString("N")) # No longer needed
    New-Item -ItemType Directory -Path $packageOutput -Force | Out-Null
    # New-Item -ItemType Directory -Path $installPath -Force | Out-Null # No longer needed
    
    # Define workspace root
    $workspaceRoot = Resolve-Path (Join-Path $PSScriptRoot "..")

    # Ensure packageOutput is cleaned up if the script is terminated prematurely
    $script:cleanupPaths = @{
        PackageOutput = $packageOutput
        # InstallPath = $null # No longer needed
    }
    
    try {
        # Pack the tool with the specific version suffix
        Write-Host "Packing Incrementalist tool (Version: $fullVersion)..."
        $packArgs = @("pack", $ProjectPath, "-c", $Configuration, "-o", $packageOutput, "/p:VersionSuffix=$suffix")
        Write-Host "Executing: dotnet $($packArgs -join ' ')"
        $packResult = Start-Process -FilePath "dotnet" -ArgumentList $packArgs -NoNewWindow -PassThru -Wait
        if ($packResult.ExitCode -ne 0) {
            throw "Failed to pack Incrementalist with exit code $($packResult.ExitCode)"
        }
        
        # Find the package
        $nupkg = Get-ChildItem -Path $packageOutput -Filter "*.nupkg" | Select-Object -First 1
        if (-not $nupkg) {
            throw "No package was created by dotnet pack"
        }
        
        # We know the package name and the exact version we built
        $packageName = "Incrementalist.Cmd" # Correct package ID
        $packageVersion = $fullVersion 

        # Ensure tool manifest exists in workspace root
        Write-Host "Ensuring tool manifest exists at $workspaceRoot..."
        $manifestResult = Start-Process -FilePath "dotnet" -ArgumentList @("new", "tool-manifest", "--force") -WorkingDirectory $workspaceRoot -NoNewWindow -PassThru -Wait
        if ($manifestResult.ExitCode -ne 0) {
            throw "Failed to create/update tool manifest with exit code $($manifestResult.ExitCode)"
        }

        # Install the tool to the manifest, specifying the exact version
        Write-Host "Installing Incrementalist tool to manifest..."
        $installArgs = @("tool", "install", "--add-source", $packageOutput, $packageName, "--version", $packageVersion)
        Write-Host "Executing: dotnet $($installArgs -join ' ') in $workspaceRoot"
        $installResult = Start-Process -FilePath "dotnet" -ArgumentList $installArgs -WorkingDirectory $workspaceRoot -NoNewWindow -PassThru -Wait
        if ($installResult.ExitCode -ne 0) {
            # Attempt uninstall just in case it was partially installed
            try {
                Write-Host "Install failed, attempting cleanup uninstall..." -ForegroundColor Yellow
                Start-Process -FilePath "dotnet" -ArgumentList @("tool", "uninstall", $packageName) -WorkingDirectory $workspaceRoot -NoNewWindow -PassThru -Wait | Out-Null
            }
            catch {
                Write-Host "Cleanup uninstall failed: $_" -ForegroundColor Yellow
            }
            throw "Failed to install Incrementalist tool to manifest with exit code $($installResult.ExitCode)"
        }
        
        $script:toolInstalled = $true
        Write-Host "Incrementalist tool installed to manifest."
    }
    catch {
        Write-Host "Error installing Incrementalist tool: $_" -ForegroundColor Red
        # Clean up both directories on error
        if (Test-Path $packageOutput) {
            Write-Host "Cleaning up package output: $packageOutput"
            Remove-Item -Path $packageOutput -Recurse -Force -ErrorAction SilentlyContinue
        }
        throw
    }
    # No finally block needed here as catch handles cleanup on error, 
    # and trap handles cleanup on exit/termination. $packageOutput 
    # doesn't need explicit cleanup on success as it's not used further.
}

function Do-CleanUp {
    Write-Host "Cleaning up environment..."
    # Define workspace root for cleanup
    $workspaceRoot = Resolve-Path (Join-Path $PSScriptRoot "..")

    # Uninstall the tool if it was installed via manifest
    if ($script:toolInstalled) {
        try {
            Write-Host "Attempting tool uninstall from manifest..."
            $uninstallResult = Start-Process -FilePath "dotnet" -ArgumentList @("tool", "uninstall", "Incrementalist.Cmd") -WorkingDirectory $workspaceRoot -NoNewWindow -PassThru -Wait
            if ($uninstallResult.ExitCode -ne 0) {
                Write-Host "Tool uninstall failed with exit code $($uninstallResult.ExitCode)" -ForegroundColor Yellow
            }
            else {
                Write-Host "Tool uninstalled successfully."
            }
        }
        catch {
            Write-Host "Error during tool uninstall: $_" -ForegroundColor Yellow
        }
    }

    # Remove the .config directory containing the manifest
    $configDir = Join-Path $workspaceRoot ".config"
    if (Test-Path $configDir) {
        Write-Host "Removing tool manifest directory: $configDir"
        Remove-Item -Path $configDir -Recurse -Force -ErrorAction SilentlyContinue
    }
   
}

# Clean up resources when the script exits
trap {
    Write-Host "Executing trap handler for script cleanup..."
    
    Do-CleanUp

    Write-Host "Trap handler finished."
     # Allow the original error to propagate if there was one
    exit $LASTEXITCODE 
}

# Abstracts how we invoke the Incrementalist executable
function Run-Incrementalist {
    param(
        [Parameter(Mandatory=$false)]
        [string]$ProjectPath,

        [ValidateSet("Release", "Debug")]
        [Parameter(Mandatory=$false)]
        [string]$Configuration = "Release",

        [Parameter(Mandatory=$false)]
        [string]$Mode = $ExecutionMode,  # Use the global parameter by default

        [Parameter(Mandatory=$false)]
        [int]$TimeoutSeconds = 120, # Default 2 minute timeout

        [Parameter(Mandatory=$true)]
        [string[]]$IncrementalistArgs       
    )

    # Initialize process variable in the function scope so it's accessible in finally block
    [System.Diagnostics.Process]$process = $null
    $exitCode = -1

    try {
        if($Mode -eq "Project"){
             # Run using dotnet run --project approach
            $cmd = "dotnet"
            $argList = @("run", "--project", $ProjectPath, "-c", $Configuration, "--no-build", "--")
             if($VerboseLogging)
             {
                 $argList += "--verbose"
             }
            $argList += $IncrementalistArgs

            # Execute the command
            $process = Start-Process -FilePath $cmd -ArgumentList $argList -NoNewWindow -PassThru -Wait
            
            # Wait with timeout
            $completed = $process.WaitForExit($TimeoutSeconds * 1000)
            if (-not $completed) {
                Write-Host "Process timed out after $TimeoutSeconds seconds" -ForegroundColor Yellow
                $process.Kill()
                return -1
            }
            
            $exitCode = $process.ExitCode
            return $exitCode
        }
        elseif ($Mode -eq "Tool") {
            # Install the tool if not already installed
            if (-not $script:toolInstalled) {
                Install-IncrementalistTool -ProjectPath $ProjectPath -Configuration $Configuration
            }
            
            $cmd = "dotnet"
            $argList = @("incrementalist")
            if($VerboseLogging)
            {
                $argList += "--verbose"
            }
            $argList += $IncrementalistArgs
            Write-Host "Executing: $($cmd) $($argList -join ' ')" -ForegroundColor Magenta
            $process = Start-Process -FilePath $cmd -ArgumentList $argList -NoNewWindow -PassThru -Wait

            # Wait with timeout
            $completed = $process.WaitForExit($TimeoutSeconds * 1000)
            if (-not $completed) {
                Write-Host "Process timed out after $TimeoutSeconds seconds" -ForegroundColor Yellow
                $process.Kill()
                return -1
            }
            
            $exitCode = $process.ExitCode
            return $exitCode
        }
        else {
            throw "Unsupported execution mode: $Mode"
        }
    }
    catch {
        Write-Host "Error executing Incrementalist: $_" -ForegroundColor Red
        return -1 # Return a standard error code
    }
    finally {
        # Ensure process is properly disposed of
        if ($process) {
            try {
                # Check if process is still running and terminate if needed
                if (-not $process.HasExited) {
                    Write-Host "Process did not exit properly - terminating..." -ForegroundColor Yellow
                    $process.Kill()
                }
                $process.Dispose()
            }
            catch {
                # Just log if we can't clean up properly
                Write-Host "Error cleaning up process resources: $_" -ForegroundColor Yellow
            }
        }
    }
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
        Run-Incrementalist -ProjectPath $ProjectPath -Configuration $Configuration -IncrementalistArgs @("list-affected-folders", "-b", "dev", "-f" , $folderTestOutput)
    }
}

function Test-SolutionCheck {
    param($ProjectPath, $Configuration, $TestResultsDir)
    
    $solutionTestOutput = Join-Path $TestResultsDir "incrementalist-affected-files.txt"
    Invoke-IncrementalistTest -TestName "Solution check (no cache)" -ProjectPath $ProjectPath -Configuration $Configuration -TestScript {
        Run-Incrementalist -ProjectPath $ProjectPath -Configuration $Configuration -IncrementalistArgs @("run", "--dry", "-b", "dev", "-f", $solutionTestOutput)
    }
}

function Test-CommandExecution {
    param($ProjectPath, $Configuration)
    
    Invoke-IncrementalistTest -TestName "Command execution (no cache)" -ProjectPath $ProjectPath -Configuration $Configuration -TestScript {
        Run-Incrementalist -ProjectPath $ProjectPath -Configuration $Configuration -IncrementalistArgs @("run", "-b", "dev", "--", "build", "-c", "Release", "--nologo")
    }
}

function Test-ParallelExecution {
    param($ProjectPath, $Configuration)
    
    Invoke-IncrementalistTest -TestName "Parallel execution (no cache)" -ProjectPath $ProjectPath -Configuration $Configuration -TestScript {
        Run-Incrementalist -ProjectPath $ProjectPath -Configuration $Configuration -IncrementalistArgs @("run", "-b", "dev", "--parallel", "--", "build", "-c", "Release", "--nologo")
    }
}

function Test-ErrorHandling {
    param($ProjectPath, $Configuration)
    
    Invoke-IncrementalistTest -TestName "Error handling (no cache)" -ProjectPath $ProjectPath -Configuration $Configuration -TestScript {
        Run-Incrementalist -ProjectPath $ProjectPath -Configuration $Configuration -IncrementalistArgs @("run", "-b", "dev", "--fail-on-no-projects", "--", "invalid-command")
    } -ExpectFailure $true
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
            "run",
            "-b", "dev",
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
        Run-Incrementalist -ProjectPath $ProjectPath -Configuration $Configuration -IncrementalistArgs @("run", "-b", "dev", "-c")
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
        $exitCodeBaseline = Run-Incrementalist -ProjectPath $ProjectPath -Configuration $Configuration -IncrementalistArgs @("run", "-b", "dev", "-f", $baselineOutput)
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
        #dotnet run --project $ProjectPath -c $Configuration --no-build -- -b dev --target-glob "**/Incrementalist.csproj" -f $targetGlobOutput
        $exitCodeTarget = Run-Incrementalist -ProjectPath $ProjectPath -Configuration $Configuration -IncrementalistArgs @("run", "--dry", "-b", "dev", "--target-glob", "**/Incrementalist.csproj", "-f", $targetGlobOutput)
        if ($exitCodeTarget -ne 0) { throw "Incrementalist command failed with exit code $exitCodeTarget" }

        # Verification logic
        $actualProjects = @(Get-Content $targetGlobOutput -ErrorAction SilentlyContinue) # Ensure it's always an array
        
        # Check if the file contains exactly one line matching the expected project string, ignoring whitespace
        if (($actualProjects | Measure-Object).Count -ne 1 -or `
            -not ($actualProjects[0].Trim() -eq $expectedProjectFullPath.Trim()) ) { # Trim() works on string now
            Write-Host "Expected output:`n$expectedProjectFullPath`nActual output:`n$($actualProjects -join "`n")" -ForegroundColor Yellow
            throw "Glob targeting verification failed. Output file content did not match expected project."
        }
        
        return 0 # Explicitly return success code
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
        $exitCodeBaseline = Run-Incrementalist -ProjectPath $ProjectPath -Configuration $Configuration -IncrementalistArgs @("run", "--dry", "-b", "dev", "-f", $baselineOutput)
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
        $exitCodeSkip = Run-Incrementalist -ProjectPath $ProjectPath -Configuration $Configuration -IncrementalistArgs @("run", "-b", "dev", "--skip-glob", "**/*.Tests.csproj", "-f", $skipGlobOutput)
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

# Test for https://github.com/petabridge/Incrementalist/issues/380
function Test-CreateConfigCustomPath {
    param($ProjectPath, $Configuration, $TestResultsDir)
    
    $customConfigFileName = "customConfig-$([guid]::NewGuid()).json" # Ensure unique name
    $customConfigPath = Join-Path $TestResultsDir $customConfigFileName # Use TestResultsDir for easier cleanup
    
    Invoke-IncrementalistTest -TestName "Create config with custom path (#380)" -ProjectPath $ProjectPath -Configuration $Configuration -TestScript {
        # Define parameters for first and second invocations - used only to run command, not check content
        $firstBaseBranch = "custom-path-first"
        $secondBaseBranch = "custom-path-second"

        # First invocation
        Write-Host "First invocation: Attempting create config at $customConfigPath"
        $exitCode1 = Run-Incrementalist -ProjectPath $ProjectPath -Configuration $Configuration -IncrementalistArgs @("create-config", "--config", $customConfigPath, "-b", $firstBaseBranch)
        if ($exitCode1 -ne 0) {
            throw "Incrementalist first command failed with exit code $exitCode1 when creating custom config."
        }

        # Second invocation (overwrite)
        Write-Host "Second invocation: Attempting overwrite config at $customConfigPath"
        # Use different/additional params to ensure command runs
        $exitCode2 = Run-Incrementalist -ProjectPath $ProjectPath -Configuration $Configuration -IncrementalistArgs @("create-config", "--config", $customConfigPath, "-b", $secondBaseBranch, "--parallel")
        if ($exitCode2 -ne 0) {
            throw "Incrementalist second command failed with exit code $exitCode2 when overwriting custom config."
        }

        # Verification: Check if the custom config file exists and is valid JSON
        if (-not (Test-Path $customConfigPath)) {
            throw "Custom configuration file was not found at $customConfigPath after second invocation."
        }

        try {
            $null = Get-Content $customConfigPath | ConvertFrom-Json
            Write-Host "Successfully parsed created/overwritten config file at $customConfigPath"
        } catch {
            throw "Failed to parse the created/overwritten configuration file at $customConfigPath as JSON: $_"
        }
        
        # Cleanup is handled by the main test cleanup for TestResultsDir
        return 0 # Success
    }
}

# Test for https://github.com/petabridge/Incrementalist/issues/381
function Test-CreateConfigOverwrite {
    param($ProjectPath, $Configuration)
    
    $tempConfigName = "temp-incrementalist-$([guid]::NewGuid()).json"
    $tempConfigPath = Join-Path (Get-Location) $tempConfigName 

    Invoke-IncrementalistTest -TestName "Create config overwrites existing specified file (#381)" -ProjectPath $ProjectPath -Configuration $Configuration -TestScript {
        # Use try/finally for reliable cleanup within the script block
        try {
            # Define parameters for first and second invocations - used only to run command
            $firstBaseBranch = "overwrite-first"
            $secondBaseBranch = "overwrite-second"

            # First invocation (create)
            Write-Host "First invocation: Attempting create config at $tempConfigPath"
            $exitCode1 = Run-Incrementalist -ProjectPath $ProjectPath -Configuration $Configuration -IncrementalistArgs @("create-config", "--config", $tempConfigPath, "-b", $firstBaseBranch)
            if ($exitCode1 -ne 0) {
                throw "Incrementalist first command failed with exit code $exitCode1 when creating config at $tempConfigPath."
            }

            # Second invocation (overwrite)
            Write-Host "Second invocation: Attempting overwrite config at $tempConfigPath"
            $exitCode2 = Run-Incrementalist -ProjectPath $ProjectPath -Configuration $Configuration -IncrementalistArgs @("create-config", "--config", $tempConfigPath, "-b", $secondBaseBranch, "--parallel")
            if ($exitCode2 -ne 0) {
                throw "Incrementalist second command failed with exit code $exitCode2 when overwriting config at $tempConfigPath."
            }

            # Verification: Check if the file exists and is valid JSON after overwrite
            if (-not (Test-Path $tempConfigPath)) {
                throw "Configuration file ($tempConfigPath) does not exist after second invocation."
            }
            
            try {
                $null = Get-Content $tempConfigPath | ConvertFrom-Json
                 Write-Host "Successfully parsed overwritten config file at $tempConfigPath"
            } catch {
                throw "Failed to parse the overwritten configuration file at $tempConfigPath as JSON: $_"
            }

            return 0 # Success
        }
        finally {
            # Cleanup: Remove the temp config file
            if (Test-Path $tempConfigPath) {
                Write-Host "Cleaning up temp config file: $tempConfigPath"
                Remove-Item -Path $tempConfigPath -Force -ErrorAction SilentlyContinue
            }
        }
    }
}

# Main execution
Write-Host "Running Incrementalist integration tests in $ExecutionMode mode..." -ForegroundColor Cyan
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

    # Run config tests
    Test-CreateConfigCustomPath -ProjectPath $project.FullName -Configuration $Configuration -TestResultsDir $testResultsDir
    Test-CreateConfigOverwrite -ProjectPath $project.FullName -Configuration $Configuration
    
    # Run all test scenarios
    Test-FoldersOnly -ProjectPath $project.FullName -Configuration $Configuration -TestResultsDir $testResultsDir
    Test-SolutionCheck -ProjectPath $project.FullName -Configuration $Configuration -TestResultsDir $testResultsDir
    Test-CommandExecution -ProjectPath $project.FullName -Configuration $Configuration
    Test-ParallelExecution -ProjectPath $project.FullName -Configuration $Configuration
    Test-ErrorHandling -ProjectPath $project.FullName -Configuration $Configuration
    Test-ComplexCommandArguments -ProjectPath $project.FullName -Configuration $Configuration
    Test-SimilarDotnetArguments -ProjectPath $project.FullName -Configuration $Configuration
    
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
    Do-CleanUp
    exit 1
}
Write-Host "`n[PASS] All integration tests completed with expected results." -ForegroundColor Green
Do-CleanUp
exit 0 