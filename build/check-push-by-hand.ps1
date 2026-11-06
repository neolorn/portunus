#Requires -Version 7.0
<#
.SYNOPSIS
    The owner's check by hand of a push on Windows (06 PRT-ENG-008, P-009, P-044).
.DESCRIPTION
    The owner runs this once on the owner's Windows PC, from the repository's root, with op
    2.41.1, the 1Password app, Docker Desktop and Windows' own OpenSSH client, and the vault
    "Sample Test" of the phase 7 report. It builds the tool and the agent from the clone, starts
    a throwaway Linux container from build/check-push/ running OpenSSH's server on 127.0.0.1:22
    and the agent in push-only mode, run through tests/Portunus.Agent.Harness with an empty
    swaps file in place of /proc/swaps (P-027), and adds the container's host key to the
    owner's known_hosts as 127.0.0.1.

    It then runs, through the real op, the 1Password app and the system's ssh: op --version,
    portunus check, portunus push (accepted with the vault's number of items), portunus status
    (ready, from a push), the same push again (refused with secrets.push.ready) and the push
    with --replace (accepted). Each command's output and error are shown and checked to hold
    none of the vault's values, which it reads with op item get --reveal into memory alone and
    never shows or writes; the agent's log is checked for the three pushes and for no value.
    Before each command it says which prompts of the 1Password app it may raise: one to allow
    op, the CLI, and one to allow an SSH key, where ssh signs in with a key the app holds. The
    owner approves each, then answers how many of each kind there were and whether anything
    like a value appeared on screen.

    It stops before anything starts where no public key is found, port 22 of 127.0.0.1 is in
    use, or the owner's known_hosts already names 127.0.0.1. At the end, whatever the outcome,
    it removes the container and its image, removes the host key with ssh-keygen -R, puts back
    the owner's own known_hosts.old or removes the one ssh-keygen wrote, and removes tmp/check-push/.

    It ends with one line: the op version, the commit checked out, passed or failed at the first
    failing step, the op prompts and the ssh prompts counted apart, and the agent's one
    difference from the shipped one. It exits 0 where the check passed and 1 otherwise.
.PARAMETER Key
    The owner's public key, which the container's login authorizes; by default the first of
    ~/.ssh/id_ed25519.pub and ~/.ssh/id_rsa.pub.
.PARAMETER Repository
    The repository to check; by default, this one.
#>
[CmdletBinding()]
param(
    [string] $Key,
    [string] $Repository = (Join-Path $PSScriptRoot '..')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$pinned = '2.41.1'
$vault = 'Sample Test'
$instance = 'sample'
$destination = 'owner@127.0.0.1'
$difference = 'the agent run through tests/Portunus.Agent.Harness with an empty swaps file'
$Repository = (Resolve-Path $Repository).Path
$work = Join-Path $Repository 'tmp' 'check-push'
$context = Join-Path $work 'context'
$knownHosts = Join-Path $HOME '.ssh' 'known_hosts'
$knownHostsOld = "$knownHosts.old"
$tool = Join-Path $Repository 'src' 'Portunus.Cli' 'bin' 'Release' 'net10.0' 'Portunus.Cli.dll'
$name = 'portunus-check-push-' + [System.Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(4)).ToLowerInvariant()

$commit = [string](& git -C $Repository rev-parse HEAD 2>$null)
if ($LASTEXITCODE -ne 0 -or -not $commit) { $commit = 'unknown' }
$version = 'none'
$opPrompts = 'no'
$sshPrompts = 'no'
$failure = $null
$started = $false
$hostKeyAdded = $false
$knownHostsHeld = $false
$knownHostsOldBefore = $null
$knownHostsBefore = $null

# Notes the first failing step; the line at the end names it.
function Set-Failed([string] $Step) {
    Write-Host "  failed: $Step"
    if (-not $script:failure) {
        $script:failure = $Step
    }
}

# Runs a program, its output and error read into memory as text, and shows them where asked; an
# op of the script's own runs as the tool runs op, less OP_SERVICE_ACCOUNT_TOKEN,
# OP_CONNECT_HOST, OP_CONNECT_TOKEN and every OP_SESSION_ variable (02 PRT-PSH-002).
function Invoke-Captured([string] $File, [string[]] $Arguments, [bool] $Show, [bool] $AsTool = $false) {
    $start = [System.Diagnostics.ProcessStartInfo]::new($File)
    foreach ($argument in $Arguments) {
        $start.ArgumentList.Add($argument)
    }

    $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    if ($AsTool) {
        foreach ($variable in @($start.Environment.Keys)) {
            if ($variable -in 'OP_SERVICE_ACCOUNT_TOKEN', 'OP_CONNECT_HOST', 'OP_CONNECT_TOKEN' -or $variable -like 'OP_SESSION_*') {
                $start.Environment.Remove($variable) | Out-Null
            }
        }
    }

    $process = [System.Diagnostics.Process]::Start($start)
    $output = $process.StandardOutput.ReadToEndAsync()
    $errors = $process.StandardError.ReadToEndAsync()
    $process.WaitForExit()
    $result = [pscustomobject]@{ ExitCode = $process.ExitCode; Output = $output.GetAwaiter().GetResult(); Error = $errors.GetAwaiter().GetResult() }
    if ($Show) {
        if ($result.Output) { Write-Host $result.Output.TrimEnd() }
        if ($result.Error) { Write-Host $result.Error.TrimEnd() }
        Write-Host "  exit code $($result.ExitCode)"
    }

    $result
}

# Runs portunus from the clone, shown and kept for the check of the vault's values, saying first
# which prompts of the 1Password app it may raise.
function Invoke-Tool([string[]] $Arguments, [string] $Prompts) {
    Write-Host "portunus $($Arguments -join ' ')"
    Write-Host "  may ask: $Prompts"
    $result = Invoke-Captured 'dotnet' (@($tool) + $Arguments) $true
    $script:shown.Add($result.Output)
    $script:shown.Add($result.Error)
    $result
}

# The one line of the agent's answer a command wrote, as JSON, or none.
function Read-Answer([string] $Text) {
    try {
        $Text.Trim() | ConvertFrom-Json
    }
    catch {
        $null
    }
}

# A member of an answer by its path, or none where the answer holds no such member.
function Read-Member([object] $Answer, [string[]] $Path) {
    foreach ($member in $Path) {
        if ($null -eq $Answer -or -not ($Answer.PSObject.Properties.Name -contains $member)) {
            return $null
        }

        $Answer = $Answer.$member
    }

    $Answer
}

# Stops before anything starts, saying why, with the line.
function Stop-Before([string] $Reason) {
    Write-Host $Reason
    Write-Host "op $version, commit $commit, failed at the start, no op prompts, no ssh prompts, $difference"
    exit 1
}

# The owner's public key, one line.
$candidates = if ($Key) { @($Key) } else { @((Join-Path $HOME '.ssh' 'id_ed25519.pub'), (Join-Path $HOME '.ssh' 'id_rsa.pub')) }
$keyFile = $candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
if (-not $keyFile) {
    Stop-Before "No public key was found ($($candidates -join ', ')): name yours with -Key <path of its .pub file>."
}

$publicKey = ([System.IO.File]::ReadAllText($keyFile)).Trim()
if ($publicKey -notmatch '^(ssh-|ecdsa-|sk-)\S+ \S+' -or $publicKey.Contains("`n")) {
    Stop-Before "$keyFile does not hold one public key."
}

foreach ($program in 'docker', 'ssh', 'ssh-keygen', 'dotnet', 'git') {
    if (-not (Get-Command $program -CommandType Application -ErrorAction SilentlyContinue)) {
        Stop-Before "$program was not found on the PATH."
    }
}

if (-not (Get-Command op -CommandType Application -ErrorAction SilentlyContinue)) {
    Stop-Before "op was not found on the PATH: install op $pinned and add its folder to the PATH, then run this again."
}

# The removal at the end takes every entry naming 127.0.0.1, so none of the owner's may be there.
if (Test-Path -LiteralPath $knownHosts) {
    & ssh-keygen -F 127.0.0.1 -f $knownHosts | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Stop-Before "$knownHosts already names 127.0.0.1; remove that entry, or run this where it holds none."
    }
}

# Port 22 of 127.0.0.1 is the container's: nothing on the PC may listen on it.
$client = [System.Net.Sockets.TcpClient]::new()
try {
    $listening = $client.ConnectAsync('127.0.0.1', 22).Wait(1000) -and $client.Connected
}
catch {
    $listening = $false
}
finally {
    $client.Dispose()
}

$free = $true
try {
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 22)
    $listener.Start()
    $listener.Stop()
}
catch {
    $free = $false
}

if ($listening -or -not $free) {
    Stop-Before 'Port 22 of 127.0.0.1 is in use on this PC; stop what listens on it, then run this again.'
}

& docker version --format '{{.Server.Version}}' *> $null
if ($LASTEXITCODE -ne 0) {
    Stop-Before 'Docker does not answer: start Docker Desktop, then run this again.'
}

Write-Host "ssh: the tool runs $((Get-Command ssh -CommandType Application | Select-Object -First 1).Source)"
$shown = [System.Collections.Generic.List[string]]::new()
try {
    Write-Host 'op --version'
    Write-Host '  may ask: to allow op'
    $version = (Invoke-Captured 'op' @('--version') $true $true).Output.Trim()
    if ($version -ne $pinned) {
        Set-Failed "op --version answers another version than $pinned"
        throw 'stop'
    }

    Write-Host 'Building the tool, and publishing the agent through the harness for linux-x64'
    dotnet build (Join-Path $Repository 'src' 'Portunus.Cli' 'Portunus.Cli.csproj') --configuration Release -p:RestoreLockedMode=true -nodeReuse:false -v q | Out-Host
    if ($LASTEXITCODE -ne 0) {
        Set-Failed 'the build of the tool'
        throw 'stop'
    }

    # A runtime's packages are not in the lockfiles, so the publish runs on a copy under tmp/
    # and leaves the repository's lockfiles as they are, as the agent's publish gate does.
    $source = Join-Path $work 'source'
    New-Item -ItemType Directory -Force -Path $source | Out-Null
    foreach ($file in 'Directory.Build.props', 'Directory.Packages.props', 'global.json', 'nuget.config', '.editorconfig') {
        Copy-Item -LiteralPath (Join-Path $Repository $file) -Destination $source
    }

    foreach ($part in 'src/Portunus.Core', 'src/Portunus.Agent', 'tests/Portunus.Agent.Harness') {
        $target = Join-Path $source $part
        New-Item -ItemType Directory -Force -Path (Split-Path $target -Parent) | Out-Null
        Copy-Item -LiteralPath (Join-Path $Repository $part) -Destination $target -Recurse
        Get-ChildItem -LiteralPath $target -Directory -Recurse | Where-Object Name -In 'bin', 'obj' | Remove-Item -Recurse -Force
    }

    dotnet publish (Join-Path $source 'tests' 'Portunus.Agent.Harness' 'Portunus.Agent.Harness.csproj') --configuration Release --runtime linux-x64 --self-contained false --output (Join-Path $context 'agent') -nodeReuse:false -v q | Out-Host
    if ($LASTEXITCODE -ne 0) {
        Set-Failed 'the publish of the agent'
        throw 'stop'
    }

    Copy-Item -Path (Join-Path $Repository 'build' 'check-push' '*') -Destination $context
    [System.IO.File]::WriteAllText((Join-Path $context 'authorized_keys'), $publicKey + "`n", [System.Text.UTF8Encoding]::new($false))

    Write-Host 'Building the container'
    $sdk = (Get-Content -Raw (Join-Path $Repository 'global.json') | ConvertFrom-Json).sdk.version
    $started = $true
    docker build --build-arg "SDK=$sdk" --tag "$($name):check" $context | Out-Host
    if ($LASTEXITCODE -ne 0) {
        Set-Failed 'the build of the container'
        throw 'stop'
    }

    Write-Host 'Starting the container on 127.0.0.1:22'
    docker run --detach --name $name --publish '127.0.0.1:22:22' "$($name):check" | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Set-Failed 'the start of the container'
        throw 'stop'
    }

    $waited = [System.Diagnostics.Stopwatch]::StartNew()
    while ($true) {
        docker exec $name test -S "/run/portunus/$instance/push/agent.sock" *> $null
        if ($LASTEXITCODE -eq 0) { break }
        if ($waited.Elapsed -gt [TimeSpan]::FromSeconds(60)) {
            # No push has run yet, so the container's log holds nothing of the vault.
            docker logs $name 2>&1 | Out-Host
            Set-Failed 'the agent in the container, which did not listen within a minute'
            throw 'stop'
        }

        Start-Sleep -Milliseconds 500
    }

    $hostKey = (docker exec $name cat /etc/ssh/ssh_host_ed25519_key.pub) -split ' '
    if ($LASTEXITCODE -ne 0 -or $hostKey.Count -lt 2) {
        Set-Failed "the container's host key"
        throw 'stop'
    }

    # known_hosts as it stood, so the end leaves it so: kept, or removed where there was none.
    $knownHostsHeld = Test-Path -LiteralPath $knownHosts
    if (Test-Path -LiteralPath $knownHostsOld) {
        $knownHostsOldBefore = [System.IO.File]::ReadAllBytes($knownHostsOld)
    }

    New-Item -ItemType Directory -Force -Path (Split-Path $knownHosts -Parent) | Out-Null
    $line = "127.0.0.1 $($hostKey[0]) $($hostKey[1])`n"
    if ($knownHostsHeld) {
        $knownHostsBefore = [System.IO.File]::ReadAllBytes($knownHosts)
        $held = [System.Text.Encoding]::UTF8.GetString($knownHostsBefore)
        if ($held.Length -gt 0 -and -not $held.EndsWith("`n")) { $line = "`n" + $line }
    }

    [System.IO.File]::AppendAllText($knownHosts, $line, [System.Text.UTF8Encoding]::new($false))
    $hostKeyAdded = $true
    Write-Host "Added the container's host key to $knownHosts as 127.0.0.1"

    Write-Host 'Approve each prompt of the 1Password app, and count those to allow op apart from those to allow an SSH key.'
    $check = Invoke-Tool @('check', '--vault', $vault) 'to allow op'
    if ($check.ExitCode -ne 0) { Set-Failed 'portunus check' }

    $first = Invoke-Tool @('push', '--vault', $vault, '--to', $destination, '--instance', $instance) 'to allow op, then to allow an SSH key'
    $firstAnswer = Read-Answer $first.Output
    if ($first.ExitCode -ne 0 -or (Read-Member $firstAnswer 'accepted') -ne $true) { Set-Failed 'portunus push' }

    $status = Invoke-Tool @('status', '--to', $destination, '--instance', $instance) 'to allow an SSH key'
    $statusAnswer = Read-Answer $status.Output
    if ($status.ExitCode -ne 0 -or (Read-Member $statusAnswer 'state') -ne 'ready' -or (Read-Member $statusAnswer 'source') -ne 'push') { Set-Failed 'portunus status' }

    $again = Invoke-Tool @('push', '--vault', $vault, '--to', $destination, '--instance', $instance) 'to allow op, then to allow an SSH key'
    $againAnswer = Read-Answer $again.Output
    if ($again.ExitCode -ne 1 -or (Read-Member $againAnswer 'accepted') -ne $false -or (Read-Member $againAnswer 'failure', 'code') -ne 'secrets.push.ready') { Set-Failed 'portunus push into a ready agent' }

    $replaced = Invoke-Tool @('push', '--vault', $vault, '--to', $destination, '--instance', $instance, '--replace') 'to allow op, then to allow an SSH key'
    $replacedAnswer = Read-Answer $replaced.Output
    if ($replaced.ExitCode -ne 0 -or (Read-Member $replacedAnswer 'accepted') -ne $true) { Set-Failed 'portunus push --replace' }

    $log = (docker logs $name 2>&1) -join "`n"

    # The vault's values, read as the tool reads them, into memory alone: never shown, never
    # written, dropped once compared. A set's .current, a version's number, and the notes are not
    # secrets, and are left out.
    Write-Host "Reading the vault's values to compare, never shown"
    Write-Host '  may ask: to allow op'
    $values = [System.Collections.Generic.List[string]]::new()
    $listed = Invoke-Captured 'op' @('item', 'list', '--vault', $vault, '--format', 'json') $false $true
    if ($listed.ExitCode -ne 0) {
        Set-Failed "op item list, the script's own"
    }
    else {
        $entries = @($listed.Output | ConvertFrom-Json)
        foreach ($entry in $entries) {
            $got = Invoke-Captured 'op' @('item', 'get', $entry.id, '--vault', $entry.vault.id, '--format', 'json', '--reveal') $false $true
            if ($got.ExitCode -ne 0) {
                Set-Failed "op item get, the script's own"
                continue
            }

            foreach ($field in @(($got.Output | ConvertFrom-Json).fields)) {
                if ($field.label -ne 'notesPlain' -and -not $field.label.EndsWith('.current') -and $field.PSObject.Properties.Name -contains 'value' -and $field.value) {
                    $values.Add([string] $field.value)
                    $values.Add([System.Buffers.Text.Base64Url]::EncodeToString([System.Text.Encoding]::UTF8.GetBytes([string] $field.value)))
                }
            }

            $got = $null
        }

        $items = $entries.Count
        foreach ($answer in $firstAnswer, $replacedAnswer) {
            if ((Read-Member $answer 'accepted') -eq $true -and (Read-Member $answer 'items') -ne $items) { Set-Failed "a push accepted with another number of items than the vault's $items" }
        }

        foreach ($text in $shown) {
            if (@($values | Where-Object { $text -and $text.Contains($_) }).Count -gt 0) { Set-Failed "a value in the tool's output" }
        }

        if (@($values | Where-Object { $log.Contains($_) }).Count -gt 0) { Set-Failed "a value in the agent's log" }

        $pushes = @([regex]::Matches($log, 'push from peer user \d+: (?<outcome>[a-z.]+), (?<items>\d+) items') | ForEach-Object { "$($_.Groups['outcome'].Value) $($_.Groups['items'].Value)" })
        if (($pushes -join ', ') -ne "accepted $items, secrets.push.ready $items, accepted $items") { Set-Failed "the agent's log of the three pushes" }
    }

    $values.Clear()
    $values = $null
    $listed = $null

    do {
        $count = Read-Host -Prompt 'How many times did the 1Password app ask you to allow op, the CLI? (a number)'
    } until ($count -match '^\d{1,3}$')
    $opPrompts = $count

    do {
        $count = Read-Host -Prompt 'How many times did the 1Password app ask you to allow an SSH key? (a number)'
    } until ($count -match '^\d{1,3}$')
    $sshPrompts = $count

    do {
        $seen = Read-Host -Prompt 'Did anything that looked like a secret value appear on the screen? (yes or no)'
    } until ($seen -in 'yes', 'no')
    if ($seen -eq 'yes') { Set-Failed 'a value on the screen, as the owner saw it' }
}
catch {
    if (-not $failure) {
        Set-Failed "an error: $($_.Exception.Message)"
    }
}
finally {
    if ($started) {
        docker rm --force $name *> $null
        docker image rm --force "$($name):check" *> $null
        Write-Host 'Removed the container and its image'
    }

    if ($hostKeyAdded) {
        & ssh-keygen -R 127.0.0.1 -f $knownHosts *> $null

        # ssh-keygen writes the file again in its own line endings: where it holds the owner's
        # entries alone, as they stood, his own bytes are put back.
        if ($null -ne $knownHostsBefore -and (Test-Path -LiteralPath $knownHosts)) {
            $after = [System.IO.File]::ReadAllText($knownHosts).Replace("`r`n", "`n").TrimEnd("`n")
            $before = [System.Text.Encoding]::UTF8.GetString($knownHostsBefore).Replace("`r`n", "`n").TrimEnd("`n")
            if ($after -ceq $before) {
                [System.IO.File]::WriteAllBytes($knownHosts, $knownHostsBefore)
            }
            else {
                Set-Failed "the removal of the container's host key, which left $knownHosts otherwise than it stood"
            }
        }

        if ($null -ne $knownHostsOldBefore) {
            [System.IO.File]::WriteAllBytes($knownHostsOld, $knownHostsOldBefore)
        }
        else {
            Remove-Item -LiteralPath $knownHostsOld -Force -ErrorAction SilentlyContinue
        }

        if (-not $knownHostsHeld) {
            Remove-Item -LiteralPath $knownHosts -Force -ErrorAction SilentlyContinue
        }

        Write-Host "Removed the container's host key from $knownHosts"
    }

    if (Test-Path -LiteralPath $work) {
        Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$outcome = if ($failure) { "failed at $failure" } else { 'passed' }
Write-Host "op $version, commit $commit, $outcome, $opPrompts op prompts, $sshPrompts ssh prompts, $difference"
exit $(if ($failure) { 1 } else { 0 })
