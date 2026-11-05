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

    It keeps everything in memory until the end, then replaces every 1Password identifier it
    recorded (a vault's, an item's, a user's, an account's, a field's and a section's) with a
    placeholder of the same length and alphabet, the same identifier always the same placeholder
    across every file, and an account's sign-in address with a placeholder address (P-035). A
    run that finds an identifier left, or either token, writes nothing, removes anything it
    wrote, and exits 1.

    It never prints a value, a token, an identifier or op's output: it prints only the files it
    writes and the exit codes. The tokens are read without echo, held only for op's environment,
    and never written.
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

# Every file, by its name, in memory until every identifier is replaced and every check passed.
$staged = [ordered]@{}

# A 1Password identifier's form: 26 ASCII letters and digits, alone between other characters.
$identifierForm = '(?<![A-Za-z0-9])[A-Za-z0-9]{26}(?![A-Za-z0-9])'

# An account's sign-in address: a name under 1Password's own domains.
$addressForm = '(?<![A-Za-z0-9.-])([A-Za-z0-9-]+)(\.1password\.(?:com|ca|eu))'

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
    $staged[$Name] = [System.Text.Encoding]::UTF8.GetString($Bytes)
    Write-Host "recorded tests/Portunus.Fakes/$Name"
}

# Gathers each identifier of a JSON document: the string of a member named id, or ending in _id,
# _by or account, of an identifier's form. An id of another form, long enough to be one, stops
# the run, since this script would not know to replace it.
function Add-Identifiers([object] $Node, [string] $Name, [System.Collections.Generic.HashSet[string]] $Found) {
    if ($Node -is [System.Collections.IEnumerable] -and $Node -isnot [string]) {
        foreach ($element in $Node) {
            Add-Identifiers $element $Name $Found
        }
    }
    elseif ($Node -is [System.Management.Automation.PSCustomObject]) {
        foreach ($member in $Node.PSObject.Properties) {
            Add-Identifiers $member.Value $member.Name $Found
        }
    }
    elseif ($Node -is [string] -and ($Name -eq 'id' -or $Name -match '(_id|_by|account)$')) {
        if ($Node -cmatch "^$identifierForm`$") {
            $Found.Add($Node) | Out-Null
        }
        elseif ($Name -eq 'id' -and $Node -cmatch '^[A-Za-z0-9]{20,}$') {
            throw 'An identifier of a form this script does not know was recorded; nothing is written.'
        }
    }
}

# A placeholder of the identifier's form: its length, in letters alone, upper case where it has
# upper case letters: x, then the identifier's number written in four letters, a for 0.
function Get-Placeholder([string] $Identifier, [int] $Number) {
    $letters = [char[]]::new(4)
    for ($index = 3; $index -ge 0; $index--) {
        $letters[$index] = [char]([int][char]'a' + ($Number % 26))
        $Number = [math]::Floor($Number / 26)
    }

    $placeholder = ('x' * ($Identifier.Length - 4)) + [string]::new($letters)
    if ($Identifier -cmatch '[A-Z]') { $placeholder.ToUpperInvariant() } else { $placeholder }
}

# Replaces every identifier and sign-in address in every staged file (P-035), checks that none,
# and neither token, is left, and only then writes the files; a check that fails after a write
# removes every file written.
function Save-Staged([string[]] $Tokens) {
    $identifiers = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($name in @($staged.Keys)) {
        if ($name -like '*.out') {
            $document = $null
            try {
                $document = $staged[$name] | ConvertFrom-Json -Depth 64
            }
            catch {
                # Not JSON: op's version, which holds no identifier.
            }

            if ($null -ne $document) {
                Add-Identifiers $document '' $identifiers
            }
        }
    }

    $replacements = [ordered]@{}
    $number = 0
    foreach ($identifier in ($identifiers | Sort-Object)) {
        $number++
        $replacements[$identifier] = Get-Placeholder $identifier $number
    }

    $addresses = @{}
    foreach ($name in @($staged.Keys)) {
        $text = $staged[$name]
        foreach ($identifier in $replacements.Keys) {
            $text = $text.Replace($identifier, $replacements[$identifier])
        }

        $text = [regex]::Replace($text, $addressForm, {
                param($match)
                $account = $match.Groups[1].Value
                if (-not $addresses.ContainsKey($account)) {
                    $addresses[$account] = 'account' + ($addresses.Count + 1).ToString([System.Globalization.CultureInfo]::InvariantCulture)
                }

                $addresses[$account] + $match.Groups[2].Value
            })
        $staged[$name] = $text
    }

    $placeholders = [System.Collections.Generic.HashSet[string]]::new([string[]] @($replacements.Values), [System.StringComparer]::Ordinal)
    foreach ($name in @($staged.Keys)) {
        $text = $staged[$name]
        foreach ($token in $Tokens) {
            if ($token -and $text.Contains($token)) {
                throw "tests/Portunus.Fakes/$name held a token; nothing is written."
            }
        }

        foreach ($match in [regex]::Matches($text, $identifierForm)) {
            if (-not $placeholders.Contains($match.Value)) {
                throw "tests/Portunus.Fakes/$name held an identifier left unreplaced; nothing is written."
            }
        }
    }

    Write-Host "  $($replacements.Count) identifiers and $($addresses.Count) sign-in addresses replaced"
    try {
        foreach ($name in $staged.Keys) {
            $path = Join-Path $fakes $name
            [System.IO.File]::WriteAllText($path, $staged[$name], [System.Text.UTF8Encoding]::new($false))
            $written.Add($path)
            Write-Host "wrote tests/Portunus.Fakes/$name"
        }

        # What was written is read again and checked again.
        foreach ($path in $written) {
            $text = [System.IO.File]::ReadAllText($path)
            $left = @([regex]::Matches($text, $identifierForm) | Where-Object { -not $placeholders.Contains($_.Value) })
            if ($left.Count -gt 0 -or @($Tokens | Where-Object { $_ -and $text.Contains($_) }).Count -gt 0) {
                throw 'A written file held an identifier or a token.'
            }
        }
    }
    catch {
        $written | ForEach-Object { Remove-Item -LiteralPath $_ -Force -ErrorAction SilentlyContinue }
        throw "$($_.Exception.Message) Every file written was removed."
    }
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

    Write-Host 'Replacing the identifiers'
    Save-Staged @($token, $revoked)
    Write-Host "Done: $($written.Count) files written under tests/Portunus.Fakes/."
}
finally {
    $token = $null
    $revoked = $null
    Remove-Item -Recurse -Force -LiteralPath $config -ErrorAction SilentlyContinue
}
