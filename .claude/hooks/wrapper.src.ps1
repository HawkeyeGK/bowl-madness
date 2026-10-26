# Readable source of the registered hook command. It is NOT run from this file: the hook
# command in .claude/settings.json is this script, minified and base64 encoded by
# New-HookCommand.ps1, so a missing or broken file on disk cannot make the hook fail open.
# __TIMEOUT_MS__ is replaced when the command is built.
#
# Rules: every outcome except "guard printed guard-ok and exited 0" or "guard printed an
# ask decision and exited 0" becomes exit code 2, which blocks the tool call. An empty or
# truncated guard.ps1 prints nothing, so it blocks.
# OWNER NOTE: files under .claude/hooks can only be changed by the owner, by hand.
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
try {
    $root = $env:CLAUDE_PROJECT_DIR
    if ([string]::IsNullOrWhiteSpace($root)) { $root = (Get-Location).Path }
    $guard = Join-Path $root '.claude\hooks\guard.ps1'
    if (-not (Test-Path -LiteralPath $guard -PathType Leaf)) { throw "guard script not found: $guard" }
    $ms = New-Object System.IO.MemoryStream
    [Console]::OpenStandardInput().CopyTo($ms)
    $inBytes = $ms.ToArray()
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $psi.Arguments = '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' + $guard + '"'
    $psi.UseShellExecute = $false
    $psi.RedirectStandardInput = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.StandardOutputEncoding = [System.Text.Encoding]::UTF8
    $psi.StandardErrorEncoding = [System.Text.Encoding]::UTF8
    $psi.CreateNoWindow = $true
    $p = [System.Diagnostics.Process]::Start($psi)
    $o = $p.StandardOutput.ReadToEndAsync()
    $e = $p.StandardError.ReadToEndAsync()
    $p.StandardInput.BaseStream.Write($inBytes, 0, $inBytes.Length)
    $p.StandardInput.Close()
    if (-not $p.WaitForExit(__TIMEOUT_MS__)) {
        try { $p.Kill() } catch { }
        throw 'guard timed out'
    }
    $p.WaitForExit()
    $out = $o.Result.Trim()
    $err = $e.Result.Trim()
    if ($p.ExitCode -eq 2) { [Console]::Error.WriteLine($err); exit 2 }
    if ($p.ExitCode -ne 0) { throw ('guard exit code ' + $p.ExitCode + ': ' + $err) }
    if ($out -ceq 'guard-ok') { exit 0 }
    $j = $out | ConvertFrom-Json
    if ($j.hookSpecificOutput.permissionDecision -ne 'ask') { throw 'unexpected guard output' }
    [Console]::Out.Write($out)
    exit 0
}
catch {
    [Console]::Error.WriteLine('Hook failed closed: ' + $_.Exception.Message)
    exit 2
}
