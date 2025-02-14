param(
    [Parameter(Mandatory=$true)]
    [string]$SignClientUser,
    
    [Parameter(Mandatory=$true)]
    [string]$SignClientSecret
)

$packages = Get-ChildItem -Path "$env:BUILD_ARTIFACTSTAGINGDIRECTORY\nuget\*.nupkg" -Exclude "*.symbols.nupkg"

foreach ($package in $packages) {
    Write-Host "Signing $($package.Name)"
    dotnet signclient sign `
        --config "$PSScriptRoot/../appsettings.json" `
        --input $package.FullName `
        --user $SignClientUser `
        --secret $SignClientSecret `
        --name "Incrementalist" `
        --description "Tool for generating incremental build data from Git diffs and Roslyn." `
        --url "https://github.com/petabridge/Incrementalist"
    
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to sign package $($package.Name)"
    }
}