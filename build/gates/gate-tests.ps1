#Requires -Version 7.0
<#
.SYNOPSIS
    Runs the unit, integration, linux and contract tests, each kind on its own by its Category trait (06 PRT-ENG-008).
.DESCRIPTION
    A test of tests/ that carries no Category trait, more than one, or a value other than
    the four kinds fails the gate before any test runs, since no run, or more than one,
    would take it. A run that finds no test of its kind passes: Microsoft.Testing.Platform's
    exit code 8, for no test run, is accepted. A repository with no test project fails, since
    the gate would run nothing. Each kind runs on the machine the gate runs on, the linux
    kind as root: under sudo on Linux, and in a container of the .NET SDK's image, whose user
    is root, on any other machine (P-027).
.PARAMETER Repository
    The repository whose tests to run; by default, this one.
#>
[CmdletBinding()]
param(
    [string] $Repository = (Join-Path $PSScriptRoot '..' '..')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$kinds = 'unit', 'integration', 'linux', 'contract'

# The linux kind runs as root, so owners, groups and chown are real (06 PRT-ENG-008, P-027).
function Invoke-AsRoot($Project) {
    $runner = Join-Path $Project.DirectoryName 'bin' 'Release' 'net10.0' $Project.BaseName
    $arguments = @('--filter-trait', 'Category=linux', '--ignore-exit-code', '8')
    if ((id -u) -eq '0') {
        & $runner @arguments
        return
    }

    $dotnet = (Get-Command dotnet).Source
    $root = if ($env:DOTNET_ROOT) { $env:DOTNET_ROOT } else { Split-Path (Resolve-Path $dotnet) }
    sudo env "PATH=$env:PATH" "DOTNET_ROOT=$root" $runner @arguments
}

# The SDK's image at the SDK of global.json, the repository copied into it without its build output.
function Invoke-LinuxContainer($Repository, $Projects) {
    $sdk = (Get-Content (Join-Path $Repository 'global.json') -Raw | ConvertFrom-Json).sdk.version
    $runs = ($Projects | ForEach-Object {
        $name = $_.BaseName
        $folder = [System.IO.Path]::GetRelativePath($Repository, $_.DirectoryName).Replace('\', '/')
        "echo '== linux tests of $name'; ./$folder/bin/Release/net10.0/$name --filter-trait Category=linux --ignore-exit-code 8 || failed=1"
    }) -join '; '
    $script = "set -u; mkdir /work && cd /src && tar --exclude=./tmp --exclude='*/bin' --exclude='*/obj' -cf - . | tar -C /work -xf - && cd /work && dotnet build Portunus.sln --configuration Release -p:RestoreLockedMode=true -nodeReuse:false -v q || exit 1; failed=0; $runs; exit `$failed"
    docker run --rm --volume "$((Resolve-Path $Repository).Path):/src:ro" "mcr.microsoft.com/dotnet/sdk:$sdk" bash -c $script | Out-Host
    return $LASTEXITCODE -eq 0
}


# The integration tests run the scanner this step leaves under tmp/tools/.
& (Join-Path $PSScriptRoot 'fetch-secret-scanner.ps1') | Out-Null

$projects = @(
    if (Test-Path (Join-Path $Repository 'tests')) {
        Get-ChildItem -Path (Join-Path $Repository 'tests') -Filter '*.Tests.csproj' -Recurse -File |
            Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
            Sort-Object FullName
    }
)
if ($projects.Count -eq 0) {
    Write-Host 'tests/ holds no test project, so the gate would run nothing.'
    throw 'The gate ran nothing.'
}

$tests = @(& (Join-Path $PSScriptRoot 'list-tests.ps1') -Repository $Repository)

$unsorted = [System.Collections.Generic.List[string]]::new()
foreach ($test in $tests | Where-Object { $_.Project -in $projects.BaseName } | Sort-Object Name -Unique) {
    $categories = @($test.Categories)
    if ($categories.Count -eq 0) {
        $unsorted.Add("$($test.Name) carries no Category trait")
    }
    elseif ($categories.Count -gt 1) {
        $unsorted.Add("$($test.Name) carries more than one Category trait: $($categories -join ', ')")
    }
    elseif ($categories[0] -cnotin $kinds) {
        $unsorted.Add("$($test.Name) carries the Category $($categories[0]), which is none of $($kinds -join ', ')")
    }
}
if ($unsorted.Count -gt 0) {
    $unsorted | ForEach-Object { Write-Host "$_." }
    throw "$($unsorted.Count) test(s) no run of a kind would take once."
}

$failures = [System.Collections.Generic.List[string]]::new()
foreach ($kind in $kinds) {
    if ($kind -eq 'linux' -and -not $IsLinux) {
        # A repository with no linux test needs no container: each project runs none.
        if (-not ($tests | Where-Object { $_.Project -in $projects.BaseName -and @($_.Categories) -ccontains 'linux' })) {
            foreach ($project in $projects) {
                Write-Host "== linux tests of $($project.BaseName)"
                Write-Host 'No test of this kind.'
            }

            continue
        }

        # On another machine the linux kind runs in a container of the SDK's image, as its root (P-027).
        Write-Host "== linux tests, in a container of the SDK's image"
        if (-not (Invoke-LinuxContainer -Repository $Repository -Projects $projects)) {
            $failures.Add('linux tests in the SDK''s container')
        }
        continue
    }

    foreach ($project in $projects) {
        Write-Host "== $kind tests of $($project.BaseName)"
        if ($kind -eq 'linux') {
            Invoke-AsRoot -Project $project
        }
        else {
            dotnet run --project $project.FullName --configuration Release --no-build -- --filter-trait "Category=$kind" --ignore-exit-code 8
        }

        if ($LASTEXITCODE -ne 0) {
            $failures.Add("$kind tests of $($project.BaseName) (exit code $LASTEXITCODE)")
        }
    }
}

if ($failures.Count -gt 0) {
    throw "Tests failed: $($failures -join '; ')."
}
