#Requires -Version 7.0
<#
.SYNOPSIS
    Checks the release compatibility of the public surface, codes, configuration keys,
    enumerations, wire formats and the executables' commands and options (04 PRT-LIB-007, 06 PRT-ENG-012).
.DESCRIPTION
    Compares them at the working tree with those the tag of the newest final release holds,
    its PublicAPI.Shipped.txt files and build/gates/release-baseline.json, a release
    candidate's tag never being that release; before the first final release the baseline is
    empty, so nothing is a break. An addition needs a minor release; a change or a removal a
    major one. On a release tag at HEAD the tag's version must be at least what changed needs, a
    release candidate's held to what its final release needs; otherwise, as on a pull request,
    a change that needs a major release fails where no commit since the newest final release
    carries ! or BREAKING CHANGE:. While nothing outside docs/ has changed since the last
    release commit, its baseline must be the release's own set (06 PRT-ENG-011). The tests of
    Portunus.Repository.Tests.ReleaseGateTests are the gate.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

& (Join-Path $PSScriptRoot 'run-repository-tests.ps1') -Classes 'Portunus.Repository.Tests.ReleaseGateTests'