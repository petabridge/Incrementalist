# Test script for Incrementalist command-line argument handling
$ErrorActionPreference = "Stop"
$containerId = $null

try {
    Write-Host "Building Incrementalist test container..."
    docker build -t incrementalist-test .

    Write-Host "`nStarting test container..."
    $containerId = docker run -d incrementalist-test tail -f /dev/null
    
    Write-Host "`nRunning tests..."
    
    # Make a change to trigger incrementalist
    docker exec $containerId /bin/bash -c "echo '// Some change' >> TestProject/Program.cs"
    
    # Test different Incrementalist commands
    $tests = @(
        @{
            Name = "Basic build command"
            Command = "incrementalist -b dev -- build"
        },
        @{
            Name = "Build with configuration"
            Command = "incrementalist -b dev -- build -c Release"
        },
        @{
            Name = "Test command"
            Command = "incrementalist -b dev -- test"
        }
    )

    $failed = $false
    foreach ($test in $tests) {
        Write-Host "`nExecuting test: $($test.Name)"
        Write-Host "Command: $($test.Command)"
        
        $result = docker exec $containerId /bin/bash -c $test.Command
        $exitCode = $LASTEXITCODE
        
        Write-Host $result
        
        if ($exitCode -ne 0) {
            Write-Host "Test failed with exit code: $exitCode" -ForegroundColor Red
            $failed = $true
        } else {
            Write-Host "Test passed" -ForegroundColor Green
        }
    }

    if ($failed) {
        throw "One or more tests failed"
    }

    Write-Host "`nAll tests completed successfully" -ForegroundColor Green
    exit 0
}
catch {
    Write-Host "Error: $_" -ForegroundColor Red
    exit 1
}
finally {
    if ($containerId) {
        Write-Host "`nCleaning up container..."
        docker stop $containerId | Out-Null
        docker rm $containerId | Out-Null
    }
} 