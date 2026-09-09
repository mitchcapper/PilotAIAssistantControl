#!/usr/bin/env pwsh
<#
.SYNOPSIS
	Bumps the NuGet package version.

.DESCRIPTION
	Directory.Build.props is the single source of version truth - both the WPF and WinUI
	packages pick <PackageVersion> up from there. This script edits that one value.

.PARAMETER Part
	Which component to increment: Major, Minor or Patch. Defaults to Patch.
	Incrementing clears any prerelease suffix (1.1.0-beta.2 -> Patch -> 1.1.1).

.PARAMETER Version
	Set an exact version instead of incrementing, e.g. 2.0.0 or 2.0.0-beta.1.

.EXAMPLE
	./bump-version.ps1
	1.0.4 -> 1.0.5

.EXAMPLE
	./bump-version.ps1 Minor
	1.0.4 -> 1.1.0

.EXAMPLE
	./bump-version.ps1 -Version 2.0.0-beta.1 -WhatIf
	Shows what would change without writing.
#>
[CmdletBinding(SupportsShouldProcess, DefaultParameterSetName = 'Part')]
param(
	[Parameter(ParameterSetName = 'Part', Position = 0)]
	[ValidateSet('Major', 'Minor', 'Patch')]
	[string]$Part = 'Patch',

	[Parameter(ParameterSetName = 'Explicit', Mandatory)]
	[ValidatePattern('^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$')]
	[string]$Version
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$propsPath = Join-Path $PSScriptRoot 'Directory.Build.props'
if (-not (Test-Path -LiteralPath $propsPath)) {
	throw "Directory.Build.props not found next to this script ($propsPath)."
}

# Read raw and swap only the version text. Round-tripping through an XML parser would
# reformat the file, and the repo keeps these files LF with no BOM.
$content = [System.IO.File]::ReadAllText($propsPath)

$match = [regex]::Match($content, '(?<open><PackageVersion>)(?<version>[^<]+)(?<close></PackageVersion>)')
if (-not $match.Success) {
	throw "No <PackageVersion> element found in $propsPath."
}

$current = $match.Groups['version'].Value.Trim()
if ($current -notmatch '^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)(?<suffix>-[0-9A-Za-z.-]+)?$') {
	throw "Current version '$current' is not a recognised x.y.z[-suffix] version."
}

if ($PSCmdlet.ParameterSetName -eq 'Explicit') {
	$new = $Version
} else {
	$major = [int]$Matches['major']
	$minor = [int]$Matches['minor']
	$patch = [int]$Matches['patch']

	switch ($Part) {
		'Major' { $major++; $minor = 0; $patch = 0 }
		'Minor' { $minor++; $patch = 0 }
		'Patch' { $patch++ }
	}

	# Incrementing always lands on a stable version - any prerelease suffix is dropped.
	$new = "$major.$minor.$patch"
}

if ($new -eq $current) {
	Write-Host "Version is already $current - nothing to do." -ForegroundColor Yellow
	return
}

Write-Host "$current -> $new" -ForegroundColor Cyan

if ($PSCmdlet.ShouldProcess($propsPath, "Set PackageVersion to $new")) {
	$updated = $content.Remove($match.Groups['version'].Index, $match.Groups['version'].Length).
		Insert($match.Groups['version'].Index, $new)

	# UTF8Encoding($false) keeps the file BOM-free; WriteAllText leaves the LF endings alone.
	[System.IO.File]::WriteAllText($propsPath, $updated, (New-Object System.Text.UTF8Encoding($false)))
	Write-Host "Updated $propsPath"
}
