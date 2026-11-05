#Requires -Version 7.0
<#
.SYNOPSIS
    Packs every package of src/ at one version into artifacts/packages/ (06 PRT-ENG-001, PRT-ENG-011).
.DESCRIPTION
    The packages are the projects of src/ but Portunus.Agent, an executable the release publishes
    and does not pack (00 section 2.2). The release job packs them at its tag's version. The
    folder is emptied of earlier packages first, and the restore is locked.
.PARAMETER Version
    The version every package carries, in place of the one MinVer would derive from the tags.
.PARAMETER Output
    The folder the packages are written to; by default, artifacts/packages/ at the repository's root.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $Version,
    [string] $Output = (Join-Path $PSScriptRoot '..' '..' 'artifacts' 'packages')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..' '..')).Path
$projects = @(Get-ChildItem -Path (Join-Path $root 'src') -Directory | Where-Object Name -CNE 'Portunus.Agent' | Sort-Object Name)
if ($projects.Count -eq 0) {
    throw 'src/ holds no package, so there is nothing to pack.'
}

New-Item -ItemType Directory -Force -Path $Output | Out-Null
Get-ChildItem -Path $Output -Filter '*.nupkg' -File | Remove-Item -Force

foreach ($project in $projects) {
    dotnet pack (Join-Path $project.FullName "$($project.Name).csproj") --configuration Release --output $Output "-p:MinVerVersionOverride=$Version" -p:RestoreLockedMode=true -nodeReuse:false
    if ($LASTEXITCODE -ne 0) {
        throw "The pack of src/$($project.Name) failed with exit code $LASTEXITCODE."
    }
}

$packed = @(Get-ChildItem -Path $Output -Filter '*.nupkg' -File | ForEach-Object Name | Sort-Object)
$expected = @($projects | ForEach-Object { "$($_.Name).$Version.nupkg" } | Sort-Object)
if (Compare-Object $packed $expected -CaseSensitive) {
    throw "The packages packed, $($packed -join ', '), are not each package of src/ at $Version."
}

Write-Host "Packed $($packed.Count) package(s) at $Version into $Output."