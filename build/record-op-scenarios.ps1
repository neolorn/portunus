#Requires -Version 7.0
<#
.SYNOPSIS
    Records the fake op's scenarios from the test vaults (06 PRT-ENG-008, P-005, P-028).
.DESCRIPTION
    The owner runs this once, on a computer with op 2.41.1 on the PATH, from the repository's
    root, after making the test vaults and service accounts the phase report lists. It runs
    exactly the commands the agent and the tool run, against the vaults "Sample Test" and
    "Sample Broken", and the error cases (a revoked token, a network that cannot be reached):
    the agent's with the test service account's token, the tool's with none, so op signs in
    through the owner's 1Password app, which prompts for it. It writes each command's output,
    error and exit code into tests/Portunus.Fakes/ in the fake's format, beside its scenario
    files. It then writes the two scenarios P-028 has written rather than recorded (a rate
    limit, and a version other than the pinned one), each marked as not recorded.

    It never prints a value, a token or op's output: it prints only the files it writes and the
    exit codes. The tokens are read without echo, held only for op's environment, and never
    written. Every file it writes is checked for both tokens, and all of them are removed if
    either is found.
.PARAMETER Repository
    The repository to write into; by default, this one.
#>
[CmdletBinding()]
param(
    [string] $Repository = (Join-Path $PSScriptRoot '..')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$pinned = '2.41.1'
$fakes = Join-Path $Repository 'tests' 'Portunus.Fakes'
$config = Join-Path $Repository 'tmp' 'record-op-scenarios'
$written = [System.Collections.Generic.List[string]]::new()

$op = (Get-Command op -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1).Source
if (-not $op) {
    throw 'No op is on the PATH.'
}

# Runs op with the arguments in this process's environment: with a token, as the agent runs it,
# less every other OP_ variable and with OP_CACHE false; with none, as the tool runs it, less
# OP_SERVICE_ACCOUNT_TOKEN, OP_CONNECT_HOST, OP_CONNECT_TOKEN and every OP_SESSION_ variable.
# Answers the exit code, the standard output's bytes and the standard error's bytes, none of them
# printed.
function Invoke-Op([string[]] $Arguments, [string] $Token) {
    $start = [System.Diagnostics.ProcessStartInfo]::new($op)
    foreach ($argument in $Arguments) {
        $start.ArgumentList.Add($argument)
    }

    $start.UseShellExecute = $false
    $start.RedirectStandardInput = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($name in @($start.Environment.Keys)) {
        $withheld = if ($Token) { $name -like 'OP_*' } else { $name -in 'OP_SERVICE_ACCOUNT_TOKEN', 'OP_CONNECT_HOST', 'OP_CONNECT_TOKEN' -or $name -like 'OP_SESSION_*' }
        if ($withheld) {
            $start.Environment.Remove($name) | Out-Null
        }
    }

    if ($Token) {
        $start.Environment['OP_CACHE'] = 'false'
        $start.Environment['OP_SERVICE_ACCOUNT_TOKEN'] = $Token
    }

    $process = [System.Diagnostics.Process]::Start($start)
    $process.StandardInput.Close()
    $output = [System.IO.MemoryStream]::new()
    $errors = [System.IO.MemoryStream]::new()
    $reading = $process.StandardOutput.BaseStream.CopyToAsync($output)
    $readingErrors = $process.StandardError.BaseStream.CopyToAsync($errors)
    $process.WaitForExit()
    [System.Threading.Tasks.Task]::WaitAll($reading, $readingErrors)
    [pscustomobject]@{ ExitCode = $process.ExitCode; Output = $output.ToArray(); Error = $errors.ToArray() }
}

# The arguments the fake matches: --config and its value left out, as the fake leaves them out.
function Get-Matched([string[]] $Arguments) {
    $matched = [System.Collections.Generic.List[string]]::new()
    for ($index = 0; $index -lt $Arguments.Count; $index++) {
        if ($Arguments[$index] -eq '--config') {
            $index++
            continue
        }

        $matched.Add($Arguments[$index])
    }

    , $matched.ToArray()
}

function Write-Fakes([string] $Name, [byte[]] $Bytes) {
    $path = Join-Path $fakes $Name
    [System.IO.File]::WriteAllBytes($path, $Bytes)
    $written.Add($path)
    Write-Host "wrote tests/Portunus.Fakes/$Name"
}

function Write-Scenario([string] $Name, [bool] $Recorded, [object[]] $Commands) {
    $scenario = [ordered]@{ recorded = $Recorded; commands = $Commands }
    $text = ($scenario | ConvertTo-Json -Depth 6) -replace "`r`n", "`n"
    Write-Fakes "$Name.json" ([System.Text.Encoding]::UTF8.GetBytes($text + "`n"))
}

# One command of a scenario: its matched arguments, the files of its output and error (none
# where it wrote nothing), its exit code, and no pause.
function New-Command([string[]] $Arguments, $Output, $ErrorFile, [int] $ExitCode) {
    [ordered]@{ arguments = $Arguments; output = $Output; error = $ErrorFile; exitCode = $ExitCode; pauseMilliseconds = 0 }
}

# Runs one command and keeps what it wrote, answering the scenario's command for it; a command
# expected to succeed that fails, or one expected to fail that succeeds, stops the recording,
# naming only its exit code.
function Save-Op([string] $Prefix, [string[]] $Arguments, [string] $Token, [bool] $MustSucceed) {
    $result = Invoke-Op $Arguments $Token
    if ($MustSucceed -ne ($result.ExitCode -eq 0)) {
        throw "op $($Arguments[0..1] -join ' ') exited $($result.ExitCode); see the phase report's list of what to make first."
    }

    $output = $null
    $errorFile = $null
    if ($result.Output.Length -gt 0) {
        $output = "$Prefix.out"
        Write-Fakes $output $result.Output
    }

    if ($result.Error.Length -gt 0) {
        $errorFile = "$Prefix.err"
        Write-Fakes $errorFile $result.Error
    }

    Write-Host "  exit code $($result.ExitCode)"
    [pscustomobject]@{ Command = (New-Command (Get-Matched $Arguments) $output $errorFile $result.ExitCode); Result = $result }
}

# Records a vault: the list, then each item. The agent's commands run with the service account's
# token and --config (01 PRT-AGT-003); the tool's run with no token and no --config, so op signs
# in through the owner's desktop app, and in the tool's order of options (02 PRT-PSH-002).
function Save-Vault([string] $Vault, [string] $Prefix, [string] $Token, [object] $Version, [bool] $Tool) {
    $commands = [System.Collections.Generic.List[object]]::new()
    $commands.Add($Version)
    $configuration = if ($Tool) { @() } else { @('--config', $config) }
    $list = Save-Op "$Prefix.list" (@('item', 'list', '--vault', $Vault, '--format', 'json') + $configuration) $Token $true
    $commands.Add($list.Command)
    $entries = @([System.Text.Encoding]::UTF8.GetString($list.Result.Output) | ConvertFrom-Json)
    $number = 0
    foreach ($entry in $entries) {
        $number++
        $arguments = if ($Tool) {
            @('item', 'get', $entry.id, '--vault', $entry.vault.id, '--format', 'json', '--reveal')
        }
        else {
            @('item', 'get', $entry.id, '--vault', $entry.vault.id, '--reveal', '--format', 'json') + $configuration
        }

        $commands.Add((Save-Op "$Prefix.item-$number" $arguments $Token $true).Command)
    }

    Write-Host "  $Vault holds $number items"
    Write-Scenario $Prefix $true $commands.ToArray()
}

function Read-Token([string] $Prompt) {
    $secure = Read-Host -Prompt $Prompt -AsSecureString
    [System.Net.NetworkCredential]::new('', $secure).Password
}

New-Item -ItemType Directory -Force -Path $config | Out-Null
$token = $null
$revoked = $null
try {
    $found = [System.Text.Encoding]::UTF8.GetString((Invoke-Op @('--version') $null).Output).Trim()
    if ($found -ne $pinned) {
        throw "op --version answers $found; the scenarios are recorded with op $pinned."
    }

    $token = Read-Token 'The token of the service account Portunus Test (not shown)'
    $revoked = Read-Token 'The token of the revoked service account Portunus Test Revoked (not shown)'

    Write-Host 'op --version'
    $version = (Save-Op 'version' @('--version') $null $true).Command

    Write-Host 'Sample Test, as the agent reads it'
    Save-Vault 'Sample Test' 'sample-test' $token $version $false
    Write-Host 'Sample Broken, as the agent reads it'
    Save-Vault 'Sample Broken' 'sample-broken' $token $version $false
    Write-Host 'Sample Test, as the tool reads it: approve each prompt of the 1Password app'
    Save-Vault 'Sample Test' 'sample-test-tool' $null $version $true
    Write-Host 'Sample Broken, as the tool reads it: approve each prompt of the 1Password app'
    Save-Vault 'Sample Broken' 'sample-broken-tool' $null $version $true

    Write-Host 'A revoked token'
    $refused = Save-Op 'refused.list' @('item', 'list', '--vault', 'Sample Test', '--format', 'json', '--config', $config) $revoked $false
    Write-Scenario 'refused' $true @($version, $refused.Command)

    Read-Host -Prompt 'Disconnect this computer from every network, then press Enter' | Out-Null
    Write-Host 'A network that cannot be reached'
    $unreachable = Save-Op 'unreachable.list' @('item', 'list', '--vault', 'Sample Test', '--format', 'json', '--config', $config) $token $false
    Read-Host -Prompt 'Reconnect this computer, then press Enter' | Out-Null
    Write-Scenario 'unreachable' $true @($version, $unreachable.Command)

    # Written, not recorded (P-028): the refused command's error with its status changed to the
    # documented (429) class, and the recorded version changed to one below the pinned one.
    Write-Host 'Written from the recordings, marked as not recorded'
    $refusedError = [System.Text.Encoding]::UTF8.GetString($refused.Result.Error)
    $limited = [regex]::Replace($refusedError, '\((401|403)\)[^\r\n]*', '(429) Too Many Requests')
    if ($limited -eq $refusedError) {
        throw 'The revoked token''s error holds neither (401) nor (403); the rate limit''s scenario is not written.'
    }

    Write-Fakes 'rate-limited.list.err' ([System.Text.Encoding]::UTF8.GetBytes($limited))
    Write-Scenario 'rate-limited' $false @($version, (New-Command (Get-Matched @('item', 'list', '--vault', 'Sample Test', '--format', 'json')) $null 'rate-limited.list.err' $refused.Result.ExitCode))
    $versionText = [System.Text.Encoding]::UTF8.GetString((Invoke-Op @('--version') $null).Output)
    Write-Fakes 'wrong-version.out' ([System.Text.Encoding]::UTF8.GetBytes($versionText.Replace($pinned, '2.41.0')))
    Write-Scenario 'wrong-version' $false @((New-Command @('--version') 'wrong-version.out' $null 0))

    # No file may hold either token; where one does, every file written is removed.
    foreach ($path in $written) {
        $text = [System.IO.File]::ReadAllText($path)
        if (($token -and $text.Contains($token)) -or ($revoked -and $text.Contains($revoked))) {
            $written | ForEach-Object { Remove-Item -LiteralPath $_ -Force }
            throw 'A written file held a token; every file written was removed.'
        }
    }

    Write-Host "Done: $($written.Count) files written under tests/Portunus.Fakes/."
}
finally {
    $token = $null
    $revoked = $null
    Remove-Item -Recurse -Force -LiteralPath $config -ErrorAction SilentlyContinue
}
