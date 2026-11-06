#Requires -Version 7.0
<#
.SYNOPSIS
    Checks the real op against the fake op's recorded scenarios (06 PRT-ENG-008, P-005, P-035, P-043).
.DESCRIPTION
    The owner runs this before each release, on a computer with op 2.41.1 on the PATH, from the
    repository's root, on the commit to be released, with the test vaults and service accounts
    the phase 7 report lists. It runs exactly the commands the agent and the tool run, against
    the vaults "Sample Test" and "Sample Broken", and the error cases (a revoked token, a network
    that cannot be reached): the agent's with the test service account's token, the tool's with
    none, so op signs in through the owner's 1Password app, which prompts for it. It records each
    command's output, error and exit code in the fake's format.

    It keeps everything in memory, then replaces every 1Password identifier it recorded (a
    vault's, an item's, a user's, an account's, a field's and a section's) with a placeholder of
    the same length and alphabet, the same identifier always the same placeholder across every
    file, and an account's sign-in address with a placeholder address (P-035), and the clone's
    own path, which op names where it reads its configuration, with <repository>. A list's
    entries, and the items read after it, are kept in the order of their identifiers, so of their
    placeholders, whatever order op lists them in. It writes the files under tmp/ first and reads
    them again; a run that finds an identifier left, the clone's path or either token, removes
    all of them and fails.

    It then compares the files with the recorded scenarios' files the commit holds under
    tests/Portunus.Fakes/, each instant set aside: the same set of files, each the same. Where
    they differ, it writes the new files in their place, removes a recorded file no longer
    recorded, builds the solution and runs the integration and linux tests on the new files
    (build/gates/gate-tests.ps1), printing their result. The written scenarios (P-028) are
    neither run nor touched.

    It ends with one line: the op version, the commit checked out, and same, changed or failed;
    where no op is on the PATH, the line says so, how to put it there, the commit and failed.
    A failed run leaves nothing behind. It never prints a value, a token, an identifier or op's
    output: only the files it records, the exit codes and its outcome. The tokens are read
    without echo, held only for op's environment, and never written. It exits 0 for same, 2 for
    changed and 1 for failed.
.PARAMETER Repository
    The repository to check; by default, this one.
#>
[CmdletBinding()]
param(
    [string] $Repository = (Join-Path $PSScriptRoot '..')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$pinned = '2.41.1'
$Repository = (Resolve-Path $Repository).Path
$fakesFolder = 'tests/Portunus.Fakes'
$fakes = Join-Path $Repository 'tests' 'Portunus.Fakes'
$work = Join-Path $Repository 'tmp' 'check-real-op'
$config = Join-Path $work 'config'
$recordedFolder = Join-Path $work 'recorded'

# Every file, by its name, in memory until every identifier is replaced and every check passed.
$staged = [ordered]@{}

# The files put in place of the committed ones, each with what the commit held, or none.
$placed = [System.Collections.Generic.List[pscustomobject]]::new()

# A 1Password identifier's form: 26 ASCII letters and digits, alone between other characters.
$identifierForm = '(?<![A-Za-z0-9])[A-Za-z0-9]{26}(?![A-Za-z0-9])'

# An account's sign-in address: a name under 1Password's own domains.
$addressForm = '(?<![A-Za-z0-9.-])([A-Za-z0-9-]+)(\.1password\.(?:com|ca|eu))'

# The clone's own path, as op may write it: as it is, with each backslash doubled as a quoted
# string holds it, and with forward slashes; replaced by a placeholder, so the clone a run is made
# from does not change what it records.
$repositoryForms = @($Repository.Replace('\', '\\'), $Repository, $Repository.Replace('\', '/')) | Select-Object -Unique
$repositoryPlaceholder = '<repository>'

# An instant as op writes one, RFC 3339, which changes at every run and is set aside.
$instantForm = '\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})'

# Runs op with the arguments in this process's environment: with a token, as the agent runs it,
# less every other OP_ variable and with OP_CACHE false; with none, as the tool runs it, less
# OP_SERVICE_ACCOUNT_TOKEN, OP_CONNECT_HOST, OP_CONNECT_TOKEN and every OP_SESSION_ variable.
# Answers the exit code, the standard output's bytes and the standard error's bytes, none of them
# printed.
function Invoke-Op([string] $Op, [string[]] $Arguments, [string] $Token) {
    $start = [System.Diagnostics.ProcessStartInfo]::new($Op)
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

function Add-Staged([string] $Name, [byte[]] $Bytes) {
    $staged[$Name] = [System.Text.Encoding]::UTF8.GetString($Bytes)
    Write-Host "recorded $Name"
}

# Gathers each identifier of a JSON document: the string of a member named id, or ending in _id,
# _by or account, of an identifier's form, or a section's id, Section_ and an identifier, whose
# identifier the fields' section ids and references hold too. An id of another form, long enough
# to be one, stops the run, since this script would not know to replace it.
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
        $section = [regex]::Match($Node, "^Section_(?<identifier>$identifierForm)`$")
        if ($Node -cmatch "^$identifierForm`$") {
            $Found.Add($Node) | Out-Null
        }
        elseif ($section.Success) {
            $Found.Add($section.Groups['identifier'].Value) | Out-Null
        }
        elseif ($Name -eq 'id' -and $Node -cmatch '^(Section_)?[A-Za-z0-9]{20,}$') {
            throw 'An identifier of a form this script does not know was recorded.'
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

# Whether a text holds an identifier other than a placeholder, either token, or the clone's path
# in any case.
function Test-Leak([string] $Text, [System.Collections.Generic.HashSet[string]] $Placeholders, [string[]] $Tokens) {
    $left = @([regex]::Matches($Text, $identifierForm) | Where-Object { -not $Placeholders.Contains($_.Value) })
    $left.Count -gt 0 -or @($Tokens | Where-Object { $_ -and $Text.Contains($_) }).Count -gt 0 -or
    @($repositoryForms | Where-Object { $Text.Contains($_, [System.StringComparison]::OrdinalIgnoreCase) }).Count -gt 0
}

# A text with the clone's path, in each of its forms and in any case, replaced by the placeholder.
function Get-WithoutRepository([string] $Text) {
    foreach ($form in $repositoryForms) {
        $Text = [regex]::Replace($Text, [regex]::Escape($form), $repositoryPlaceholder, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    }

    $Text
}

# A list's text with its entries in the order of their identifiers, as the replacement numbers
# them: each entry's own text kept as op wrote it, between the text op wrote around and between
# them.
function Get-ListInOrder([string] $Text) {
    $document = [System.Text.Json.JsonDocument]::Parse($Text)
    try {
        $entries = @(foreach ($element in $document.RootElement.EnumerateArray()) {
                [pscustomobject]@{ Id = $element.GetProperty('id').GetString(); Raw = $element.GetRawText() }
            })
    }
    finally {
        $document.Dispose()
    }

    if ($entries.Count -lt 2) {
        return $Text
    }

    $starts = [System.Collections.Generic.List[int]]::new()
    $cursor = 0
    foreach ($entry in $entries) {
        $start = $Text.IndexOf($entry.Raw, $cursor, [System.StringComparison]::Ordinal)
        $starts.Add($start)
        $cursor = $start + $entry.Raw.Length
    }

    $between = $Text.Substring($starts[0] + $entries[0].Raw.Length, $starts[1] - $starts[0] - $entries[0].Raw.Length)
    $before = $Text.Substring(0, $starts[0])
    $after = $Text.Substring($cursor)
    $before + ((@($entries | Sort-Object -Property Id) | ForEach-Object Raw) -join $between) + $after
}

# Replaces every identifier and sign-in address in every staged file (P-035), checks that none,
# and neither token, is left, then writes the files under tmp/ and reads them again; a finding
# removes every file written there.
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
        $text = Get-WithoutRepository $staged[$name]
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
        if (Test-Leak $staged[$name] $placeholders $Tokens) {
            throw "$name held an identifier left unreplaced, the clone's path or a token."
        }
    }

    Write-Host "  $($replacements.Count) identifiers and $($addresses.Count) sign-in addresses replaced"
    New-Item -ItemType Directory -Force -Path $recordedFolder | Out-Null
    foreach ($name in $staged.Keys) {
        [System.IO.File]::WriteAllText((Join-Path $recordedFolder $name), $staged[$name], [System.Text.UTF8Encoding]::new($false))
    }

    # What was written is read again and checked again.
    foreach ($name in $staged.Keys) {
        if (Test-Leak ([System.IO.File]::ReadAllText((Join-Path $recordedFolder $name))) $placeholders $Tokens) {
            throw "The file written for $name held an identifier, the clone's path or a token."
        }
    }
}

function Add-Scenario([string] $Name, [object[]] $Commands) {
    $scenario = [ordered]@{ recorded = $true; commands = $Commands }
    $text = ($scenario | ConvertTo-Json -Depth 6) -replace "`r`n", "`n"
    Add-Staged "$Name.json" ([System.Text.Encoding]::UTF8.GetBytes($text + "`n"))
}

# One command of a scenario: its matched arguments, the files of its output and error (none
# where it wrote nothing), its exit code, and no pause.
function New-Command([string[]] $Arguments, $Output, $ErrorFile, [int] $ExitCode) {
    [ordered]@{ arguments = $Arguments; output = $Output; error = $ErrorFile; exitCode = $ExitCode; pauseMilliseconds = 0 }
}

# Runs one command and keeps what it wrote, answering the scenario's command for it; a command
# expected to succeed that fails, or one expected to fail that succeeds, stops the run, naming
# only its exit code.
function Save-Op([string] $Op, [string] $Prefix, [string[]] $Arguments, [string] $Token, [bool] $MustSucceed) {
    $result = Invoke-Op $Op $Arguments $Token
    if ($MustSucceed -ne ($result.ExitCode -eq 0)) {
        throw "op $($Arguments[0..1] -join ' ') exited $($result.ExitCode); see the phase 7 report's list of what to make first."
    }

    $output = $null
    $errorFile = $null
    if ($result.Output.Length -gt 0) {
        $output = "$Prefix.out"
        Add-Staged $output $result.Output
    }

    if ($result.Error.Length -gt 0) {
        $errorFile = "$Prefix.err"
        Add-Staged $errorFile $result.Error
    }

    Write-Host "  exit code $($result.ExitCode)"
    [pscustomobject]@{ Command = (New-Command (Get-Matched $Arguments) $output $errorFile $result.ExitCode); Result = $result }
}

# Records a vault: the list, its entries kept in the order of their identifiers, then each item in
# that order. The agent's commands run with the service account's token and --config (01
# PRT-AGT-003); the tool's run with no token and no --config, so op signs in through the owner's
# desktop app, and in the tool's order of options (02 PRT-PSH-002).
function Save-Vault([string] $Op, [string] $Vault, [string] $Prefix, [string] $Token, [object] $Version, [bool] $Tool) {
    $commands = [System.Collections.Generic.List[object]]::new()
    $commands.Add($Version)
    $configuration = if ($Tool) { @() } else { @('--config', $config) }
    $list = Save-Op $Op "$Prefix.list" (@('item', 'list', '--vault', $Vault, '--format', 'json') + $configuration) $Token $true
    $commands.Add($list.Command)
    $staged["$Prefix.list.out"] = Get-ListInOrder $staged["$Prefix.list.out"]
    $entries = @([System.Text.Encoding]::UTF8.GetString($list.Result.Output) | ConvertFrom-Json | Sort-Object -Property id)
    $number = 0
    foreach ($entry in $entries) {
        $number++
        $arguments = if ($Tool) {
            @('item', 'get', $entry.id, '--vault', $entry.vault.id, '--format', 'json', '--reveal')
        }
        else {
            @('item', 'get', $entry.id, '--vault', $entry.vault.id, '--reveal', '--format', 'json') + $configuration
        }

        $commands.Add((Save-Op $Op "$Prefix.item-$number" $arguments $Token $true).Command)
    }

    Write-Host "  $Vault holds $number items"
    Add-Scenario $Prefix $commands.ToArray()
}

function Read-Token([string] $Prompt) {
    $secure = Read-Host -Prompt $Prompt -AsSecureString
    [System.Net.NetworkCredential]::new('', $secure).Password
}

# The files of the recorded scenarios the commit holds: each scenario marked recorded, and the
# files its commands name. A written scenario (P-028), and a file only it names, is left out.
function Get-CommittedRecorded {
    $files = [ordered]@{}
    $names = @(& git -C $Repository ls-tree --name-only HEAD "$fakesFolder/")
    if ($LASTEXITCODE -ne 0) { throw 'git ls-tree failed.' }
    foreach ($path in $names | Where-Object { $_ -like '*.json' -and $_ -notlike '*/packages.lock.json' }) {
        $text = (& git -C $Repository show "HEAD:$path") -join "`n"
        if ($LASTEXITCODE -ne 0) { throw "git show HEAD:$path failed." }
        $scenario = $text | ConvertFrom-Json
        if (-not ($scenario.PSObject.Properties.Name -contains 'recorded') -or -not $scenario.recorded) {
            continue
        }

        $files[[System.IO.Path]::GetFileName($path)] = $true
        foreach ($command in @($scenario.commands)) {
            foreach ($file in @($command.output, $command.error) | Where-Object { $_ }) {
                $files[$file] = $true
            }
        }
    }

    foreach ($name in @($files.Keys)) {
        $text = & git -C $Repository show "HEAD:$fakesFolder/$name" 2>$null
        $files[$name] = if ($LASTEXITCODE -eq 0) { ($text -join "`n") } else { $null }
    }

    $files
}

# A file's text as compared: its line endings as line feeds, its final ones dropped, and each
# instant set aside.
function Get-Compared([string] $Text) {
    [regex]::Replace($Text.Replace("`r`n", "`n").TrimEnd("`n"), $instantForm, '<instant>')
}

# Whether the recorded files are those the commit holds, by name and by content.
function Test-Same([System.Collections.Specialized.OrderedDictionary] $Committed) {
    $differ = $false
    foreach ($name in @($staged.Keys) + @($Committed.Keys) | Sort-Object -Unique) {
        if (-not $staged.Contains($name)) {
            Write-Host "  $name is no longer recorded"
            $differ = $true
        }
        elseif (-not $Committed.Contains($name) -or $null -eq $Committed[$name]) {
            Write-Host "  $name is new"
            $differ = $true
        }
        elseif ((Get-Compared $staged[$name]) -cne (Get-Compared $Committed[$name])) {
            Write-Host "  $name differs"
            $differ = $true
        }
    }

    -not $differ
}

# Puts the recorded files in place of the committed ones, and removes a committed recorded file
# no longer recorded, keeping what each held so a failure puts it back.
function Set-InPlace([System.Collections.Specialized.OrderedDictionary] $Committed) {
    foreach ($name in $staged.Keys) {
        $path = Join-Path $fakes $name
        $before = if (Test-Path -LiteralPath $path) { [System.IO.File]::ReadAllBytes($path) } else { $null }
        $placed.Add([pscustomobject]@{ Path = $path; Before = $before })
        Copy-Item -LiteralPath (Join-Path $recordedFolder $name) -Destination $path -Force
        Write-Host "wrote $fakesFolder/$name"
    }

    foreach ($name in $Committed.Keys | Where-Object { -not $staged.Contains($_) }) {
        $path = Join-Path $fakes $name
        if (Test-Path -LiteralPath $path) {
            $placed.Add([pscustomobject]@{ Path = $path; Before = [System.IO.File]::ReadAllBytes($path) })
            Remove-Item -LiteralPath $path -Force
            Write-Host "removed $fakesFolder/$name"
        }
    }
}

# Puts back what each file put in place held before, removing one the commit did not hold.
function Undo-InPlace {
    foreach ($file in $placed) {
        if ($null -eq $file.Before) {
            Remove-Item -LiteralPath $file.Path -Force -ErrorAction SilentlyContinue
        }
        else {
            [System.IO.File]::WriteAllBytes($file.Path, $file.Before)
        }
    }

    $placed.Clear()
}

$commit = [string](& git -C $Repository rev-parse HEAD 2>$null)
if ($LASTEXITCODE -ne 0 -or -not $commit) { $commit = 'unknown' }
$version = 'none'
$outcome = 'failed'
$missing = $false
$token = $null
$revoked = $null
try {
    $found = @(Get-Command op -CommandType Application -ErrorAction SilentlyContinue)
    if ($found.Count -eq 0) {
        $missing = $true
        throw 'No op is on the PATH.'
    }

    $op = $found[0].Source

    $version = [System.Text.Encoding]::UTF8.GetString((Invoke-Op $op @('--version') $null).Output).Trim()
    if ($version -ne $pinned) {
        throw "op --version answers another version than $pinned, which the agent pins."
    }

    if (Test-Path -LiteralPath $work) {
        Remove-Item -Recurse -Force -LiteralPath $work
    }

    New-Item -ItemType Directory -Force -Path $config | Out-Null
    $token = Read-Token 'The token of the service account Portunus Test (not shown)'
    $revoked = Read-Token 'The token of the revoked service account Portunus Test Revoked (not shown)'

    Write-Host 'op --version'
    $versionCommand = (Save-Op $op 'version' @('--version') $null $true).Command

    Write-Host 'Sample Test, as the agent reads it'
    Save-Vault $op 'Sample Test' 'sample-test' $token $versionCommand $false
    Write-Host 'Sample Broken, as the agent reads it'
    Save-Vault $op 'Sample Broken' 'sample-broken' $token $versionCommand $false
    Write-Host 'Sample Test, as the tool reads it: approve each prompt of the 1Password app'
    Save-Vault $op 'Sample Test' 'sample-test-tool' $null $versionCommand $true
    Write-Host 'Sample Broken, as the tool reads it: approve each prompt of the 1Password app'
    Save-Vault $op 'Sample Broken' 'sample-broken-tool' $null $versionCommand $true

    Write-Host 'A revoked token'
    $refused = Save-Op $op 'refused.list' @('item', 'list', '--vault', 'Sample Test', '--format', 'json', '--config', $config) $revoked $false
    Add-Scenario 'refused' @($versionCommand, $refused.Command)

    Read-Host -Prompt 'Disconnect this computer from every network, then press Enter' | Out-Null
    Write-Host 'A network that cannot be reached'
    $unreachable = Save-Op $op 'unreachable.list' @('item', 'list', '--vault', 'Sample Test', '--format', 'json', '--config', $config) $token $false
    Read-Host -Prompt 'Reconnect this computer, then press Enter' | Out-Null
    Add-Scenario 'unreachable' @($versionCommand, $unreachable.Command)

    Write-Host 'Replacing the identifiers'
    Save-Staged @($token, $revoked)
    $token = $null
    $revoked = $null

    Write-Host "Comparing with $fakesFolder/ at the commit"
    $committed = Get-CommittedRecorded
    if (Test-Same $committed) {
        $outcome = 'same'
    }
    else {
        Set-InPlace $committed
        $outcome = 'changed'
        Write-Host 'The integration and linux tests, on the new files'
        $tests = 'passed'
        try {
            # The solution is built first, as the gates build it, since the tests start the fakes and the harness it builds.
            dotnet build (Join-Path $Repository 'Portunus.sln') --configuration Release -p:RestoreLockedMode=true -nodeReuse:false | Out-Host
            if ($LASTEXITCODE -ne 0) {
                throw "The build of Portunus.sln failed with exit code $LASTEXITCODE."
            }

            & (Join-Path $Repository 'build' 'gates' 'gate-tests.ps1') -Repository $Repository -Kinds 'integration', 'linux' | Out-Host
        }
        catch {
            $tests = 'failed'
        }

        Write-Host "The integration and linux tests $tests."
    }
}
catch {
    if (-not $missing) {
        Write-Host $_.Exception.Message
    }

    Undo-InPlace
    $outcome = 'failed'
}
finally {
    $token = $null
    $revoked = $null
    Remove-Item -Recurse -Force -LiteralPath $work -ErrorAction SilentlyContinue
}

if ($missing) {
    Write-Host "op was not found on the PATH: install op $pinned and add its folder to the PATH, then run this again; commit $commit, failed"
}
else {
    Write-Host "op $version, commit $commit, $outcome"
}
exit $(switch ($outcome) { 'same' { 0 } 'changed' { 2 } default { 1 } })
