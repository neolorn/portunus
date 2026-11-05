#Requires -Version 7.0
<#
.SYNOPSIS
    Checks the packages against the allow-list of 06 PRT-ENG-009.
.DESCRIPTION
    Runs the repository's tests of the allow-list, which read every project and its lockfile
    and the central package versions.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

& (Join-Path $PSScriptRoot 'run-repository-tests.ps1') -Classes 'Portunus.Repository.Tests.PackageAllowListTests'
