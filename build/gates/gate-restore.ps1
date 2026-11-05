#Requires -Version 7.0
<#
.SYNOPSIS
    Restores every project in locked mode, with NuGet's audit of every package (06 PRT-ENG-002, PRT-ENG-010).
.DESCRIPTION
    A restore that would change a lockfile fails, and so does a package, transitive ones
    included, with a published advisory, since the audit's warnings are errors.
.PARAMETER Repository
    The repository whose Portunus.sln to restore; by default, this one.
#>
[CmdletBinding()]
param(
    [string] $Repository = (Join-Path $PSScriptRoot '..' '..')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$solution = Join-Path $Repository 'Portunus.sln'
if (-not (Test-Path $solution)) {
    throw 'Portunus.sln is missing.'
}

dotnet restore $solution --locked-mode --force
if ($LASTEXITCODE -ne 0) {
    throw "The locked restore or its audit failed with exit code $LASTEXITCODE."
}
