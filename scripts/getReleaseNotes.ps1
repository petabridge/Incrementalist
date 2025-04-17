function Get-ReleaseNotes
{
    param (
        [Parameter(Mandatory = $true)]
        [string]$MarkdownFile
    )

    # Read markdown file content
    $content = Get-Content -Path $MarkdownFile -Raw

    # Split content based on headers
    $sections = $content -split "####"

    # Output object to store result
    $outputObject = [PSCustomObject]@{
        Version = $null
        Date = $null
        ReleaseNotes = $null
    }

    # Check if we have at least 3 sections (1. Before the header, 2. Header, 3. Release notes)
    if ($sections.Count -ge 3)
    {
        $header = $sections[1].Trim()
        $releaseNotes = $sections[2].Trim()

        # Extract version and date from the header
        $headerParts = $header -split " ", 2
        if ($headerParts.Count -eq 2)
        {
            $outputObject.Version = $headerParts[0]
            $outputObject.Date = $headerParts[1]
        }

        $outputObject.ReleaseNotes = $releaseNotes
    }

    # Return the output object
    return $outputObject
}

function Update-ProjectVersion
{
    param (
        [string]$Version,
        [string]$Notes,
        [string]$PropsFile
    )

    if (-not (Test-Path $PropsFile))
    {
        throw "Project properties file not found at: $PropsFile"
    }

    $xml = [xml](Get-Content $PropsFile)
    $versionPrefix = $xml.SelectSingleNode("//VersionPrefix")
    if ($versionPrefix)
    {
        $versionPrefix.InnerText = $Version
    }

    $releaseNotes = $xml.SelectSingleNode("//PackageReleaseNotes")
    if ($releaseNotes)
    {
        $releaseNotes.InnerText = $Notes
    }

    $xml.Save($PropsFile)
} 