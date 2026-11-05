#Requires -Version 7.0
<#
.SYNOPSIS
    Checks the projects' layout and references (06 PRT-ENG-001, 04 PRT-LIB-001).
.DESCRIPTION
    Runs the repository's tests of the layout and the project references, which read every
    project of the repository.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

& (Join-Path $PSScriptRoot 'run-repository-tests.ps1') -Classes 'Portunus.Repository.Tests.RepositoryLayoutTests', 'Portunus.Repository.Tests.ProjectReferenceTests'
