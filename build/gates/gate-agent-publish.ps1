#Requires -Version 7.0
<#
.SYNOPSIS
    Publishes the agent self-contained and single-file for linux-x64 and linux-arm64 (06 PRT-ENG-011, PRT-ENG-012).
.DESCRIPTION
    The runtime identifier, self-contained and single-file publication are arguments of the
    publish, never properties of the project (06 PRT-ENG-002). A runtime's packages are not in
    the lockfiles, which the restore gate checks, so the publish runs on a copy under tmp/
    and leaves the repository as it found it.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..' '..')).Path

# The publish works on a copy under tmp/, so the restores it makes for each runtime leave the
# repository's lockfiles as they are.
$copy = Join-Path $root 'tmp' 'publish' 'source'
if (Test-Path $copy) {
    Remove-Item $copy -Recurse -Force
}

New-Item -ItemType Directory -Force (Join-Path $copy 'src') | Out-Null
foreach ($file in 'Directory.Build.props', 'Directory.Packages.props', 'global.json', 'nuget.config', '.editorconfig') {
    Copy-Item (Join-Path $root $file) $copy
}

foreach ($part in 'Portunus.Core', 'Portunus.Agent') {
    Copy-Item (Join-Path $root 'src' $part) (Join-Path $copy 'src') -Recurse -Exclude 'bin', 'obj'
    Get-ChildItem (Join-Path $copy 'src' $part) -Directory -Recurse | Where-Object Name -In 'bin', 'obj' | Remove-Item -Recurse -Force
}

$project = Join-Path $copy 'src' 'Portunus.Agent' 'Portunus.Agent.csproj'

foreach ($runtime in 'linux-x64', 'linux-arm64') {
    $output = Join-Path $root 'tmp' 'publish' $runtime
    dotnet publish $project --configuration Release --runtime $runtime --self-contained true -p:PublishSingleFile=true --output $output -nodeReuse:false
    if ($LASTEXITCODE -ne 0) {
        throw "The agent's publish for $runtime failed with exit code $LASTEXITCODE."
    }

    $executable = Join-Path $output 'Portunus.Agent'
    if (-not (Test-Path $executable -PathType Leaf)) {
        throw "The agent's publish for $runtime left no single executable at $executable."
    }
}
