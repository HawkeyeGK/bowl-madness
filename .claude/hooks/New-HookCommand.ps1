# Prints the hook command string for .claude/settings.json (or the plain minified source
# with -SourceOnly). The command runs powershell.exe with the wrapper as -EncodedCommand,
# so there is no quoting to get wrong in either bash or PowerShell shells.
# OWNER NOTE: files under .claude/hooks can only be changed by the owner, by hand.
param([int]$TimeoutMs = 30000, [switch]$SourceOnly)
$src = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'wrapper.src.ps1') -Raw -Encoding UTF8
$lines = $src -split "`r?`n" | Where-Object { $_.Trim() -ne '' -and -not $_.TrimStart().StartsWith('#') } | ForEach-Object { $_.Trim() }
$min = ($lines -join "`n").Replace('__TIMEOUT_MS__', [string]$TimeoutMs)
if ($SourceOnly) { return $min }
$b64 = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($min))
'C:/Windows/System32/WindowsPowerShell/v1.0/powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand ' + $b64
