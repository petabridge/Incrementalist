#!/usr/bin/env bash
set -e

# Default to "Help" if no target provided
target=${1:-"Help"}
configuration=${2:-"Release"}
version_suffix=$3
nuget_api_key=$4
nuget_source=$5
sign_client_secret=$6
sign_client_user=$7

# Convert to lowercase for case-insensitive comparison
target=$(echo "$target" | tr '[:upper:]' '[:lower:]')

# Execute PowerShell script with parameters
pwsh ./build.ps1 -Target "$target" \
                 -Configuration "$configuration" \
                 -VersionSuffix "$version_suffix" \
                 -NugetApiKey "$nuget_api_key" \
                 -NugetSource "$nuget_source" \
                 -SignClientSecret "$sign_client_secret" \
                 -SignClientUser "$sign_client_user"