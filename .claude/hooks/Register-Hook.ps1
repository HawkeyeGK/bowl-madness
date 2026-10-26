# Run this yourself, in your own terminal, from the repo root, after copying the draft files into
# .claude\hooks. Copy this file there too. It reads only the installed files next to it
# (New-HookCommand.ps1, wrapper.src.ps1, guard.ps1, test-guard.ps1), never the drafts.
# It sets the matcher, the registered command and the timeout of the one hook entry in
# .claude\settings.json, rewrites the file with two-space indentation, and checks the result parses
# and holds the same data. It writes a temp file and swaps it in, and keeps the first .bak copy
# (later runs do not overwrite it). Nothing else in the file changes.
#   powershell -NoProfile -File .claude\hooks\Register-Hook.ps1
# Use -WhatIf to see what would change without writing. -Settings points at another copy of the file.
[CmdletBinding(SupportsShouldProcess)]
param([string]$Settings, [int]$TimeoutMs = 30000, [int]$HookTimeoutSec = 45)
$ErrorActionPreference = 'Stop'
# $PSScriptRoot is empty inside a param-block default on Windows PowerShell 5.1, so resolve the folder here
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $Settings) { $Settings = Join-Path $here '..\settings.json' }
$matcher = 'Bash|PowerShell|Monitor|Write|Edit|MultiEdit|NotebookEdit|mcp__terminal__run_in_terminal'
foreach ($f in 'New-HookCommand.ps1', 'wrapper.src.ps1', 'guard.ps1', 'test-guard.ps1') {
    if (-not (Test-Path -LiteralPath (Join-Path $here $f))) { throw "$f is not in $here. Copy the draft files there first." }
}
$cmd = & (Join-Path $here 'New-HookCommand.ps1') -TimeoutMs $TimeoutMs
$path = (Resolve-Path -LiteralPath $Settings).Path
$text = [IO.File]::ReadAllText($path)
$obj = $text | ConvertFrom-Json

# find the one hook entry that runs the encoded wrapper, or add it
$entries = @()
if ($obj.PSObject.Properties['hooks'] -and $obj.hooks.PSObject.Properties['PreToolUse']) {
    $entries = @($obj.hooks.PreToolUse | Where-Object { @($_.hooks | Where-Object { $_.command -like '*-EncodedCommand*' }).Count -gt 0 })
}
if ($entries.Count -gt 1) { throw 'more than one registered hook entry in settings.json; fix by hand' }
if ($entries.Count -eq 0) {
    $entry = [pscustomobject]@{ matcher = $matcher; hooks = @([pscustomobject]@{ type = 'command'; command = $cmd; timeout = $HookTimeoutSec }) }
    if (-not $obj.PSObject.Properties['hooks']) { $obj | Add-Member -NotePropertyName hooks -NotePropertyValue ([pscustomobject]@{}) }
    if ($obj.hooks.PSObject.Properties['PreToolUse']) { $obj.hooks.PreToolUse = @($obj.hooks.PreToolUse) + $entry }
    else { $obj.hooks | Add-Member -NotePropertyName PreToolUse -NotePropertyValue @($entry) }
}
else {
    $entries[0].matcher = $matcher
    $h = @($entries[0].hooks | Where-Object { $_.command -like '*-EncodedCommand*' })[0]
    $h.command = $cmd
    $h.timeout = $HookTimeoutSec
}

# two-space JSON writer (Windows PowerShell 5.1 ConvertTo-Json indents by four and aligns oddly)
function Format-Str([string]$s) {
    $e = $s.Replace('\', '\\').Replace('"', '\"').Replace("`n", '\n').Replace("`r", '\r').Replace("`t", '\t')
    '"' + [regex]::Replace($e, '[\x00-\x1f]', { param($m) '\u{0:x4}' -f [int][char]$m.Value }) + '"'
}
function Format-Json($v, [int]$level) {
    $pad = '  ' * ($level + 1); $end = '  ' * $level
    if ($null -eq $v) { return 'null' }
    if ($v -is [string]) { return Format-Str $v }
    if ($v -is [bool]) { return $(if ($v) { 'true' } else { 'false' }) }
    if ($v -is [ValueType]) { return [string]::Format([Globalization.CultureInfo]::InvariantCulture, '{0}', $v) }
    if ($v -is [System.Collections.IEnumerable]) {
        $items = @($v | ForEach-Object { $pad + (Format-Json $_ ($level + 1)) })
        if ($items.Count -eq 0) { return '[]' }
        return "[`n" + ($items -join ",`n") + "`n$end]"
    }
    $props = @($v.PSObject.Properties | ForEach-Object { $pad + (Format-Str $_.Name) + ': ' + (Format-Json $_.Value ($level + 1)) })
    if ($props.Count -eq 0) { return '{}' }
    return "{`n" + ($props -join ",`n") + "`n$end}"
}
$nl = if ($text.Contains("`r`n")) { "`r`n" } else { "`n" }
$new = ((Format-Json $obj 0) + "`n").Replace("`n", $nl)

# the rewrite must hold the same data as the object, and the registration must have taken
$back = $new | ConvertFrom-Json
if (($back | ConvertTo-Json -Depth 20 -Compress) -cne ($obj | ConvertTo-Json -Depth 20 -Compress)) { throw 'rewritten file does not hold the same data; nothing written' }
$reg = @($back.hooks.PreToolUse | Where-Object { $_.matcher -ceq $matcher })
if ($reg.Count -ne 1 -or $reg[0].hooks[0].command -cne $cmd) { throw 'registration did not take; nothing written' }

if ($new -ceq $text) { 'settings.json already matches; nothing to change.'; return }
'matcher: ' + $matcher
'command: ' + $cmd.Length + ' characters, hook timeout ' + $HookTimeoutSec + ' s'
if ($PSCmdlet.ShouldProcess($path, 'rewrite with two-space indentation and the registered hook')) {
    $bak = $path + '.bak'
    if (-not (Test-Path -LiteralPath $bak)) { Copy-Item -LiteralPath $path -Destination $bak }   # the first .bak is the original; later runs keep it
    $tmp = $path + '.tmp'
    [IO.File]::WriteAllText($tmp, $new, (New-Object System.Text.UTF8Encoding($false)))
    if ([IO.File]::ReadAllText($tmp) -cne $new) { Remove-Item -LiteralPath $tmp; throw 'temp file did not hold the new text; settings.json untouched' }
    Move-Item -LiteralPath $tmp -Destination $path -Force   # swap into place; a crash leaves the old file, never a half-written one
    'Updated ' + $path + ' (original kept as ' + $bak + ')'
}
