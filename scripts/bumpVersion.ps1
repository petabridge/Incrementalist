function UpdateVersionAndReleaseNotes {
    param (
        [Parameter(Mandatory=$true)]
        [PSCustomObject]$ReleaseNotesResult,

        [Parameter(Mandatory=$true)]
        [string]$XmlFilePath
    )

    if (-not (Test-Path $XmlFilePath)) {
        throw "Directory.Build.props not found at: $XmlFilePath"
    }

    try {
        # Load XML
        $xmlContent = New-Object XML
        $xmlContent.Load($XmlFilePath)

        # Update VersionPrefix and PackageReleaseNotes
        $versionPrefixElement = $xmlContent.SelectSingleNode("//VersionPrefix")
        $versionPrefixElement.InnerText = $ReleaseNotesResult.Version

        $packageReleaseNotesElement = $xmlContent.SelectSingleNode("//PackageReleaseNotes")
        $packageReleaseNotesElement.InnerText = $ReleaseNotesResult.ReleaseNotes

        # Save the updated XML
        $xmlContent.Save($XmlFilePath)
    }
    catch {
        throw "Failed to update Directory.Build.props: $_"
    }
} 