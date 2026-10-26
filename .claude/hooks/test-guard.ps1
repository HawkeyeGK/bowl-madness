# Tests for the PreToolUse guard. Run from anywhere:
#   powershell -NoProfile -File .claude/hooks-draft/test-guard.ps1     (draft)
#   powershell -NoProfile -File .claude/hooks/test-guard.ps1           (installed)
# The script builds a fixture project in a temp folder, copies the guard that sits next to this
# script into it, and sends every case through the real hook path: wrapper (encoded command)
# -> guard.ps1. Exit code is 0 only when every case behaves as expected.
# A refuse or ask case in the rules group also has to name its own rule id (the part of the case
# id before the first dash), so a different rule cannot make it pass by accident.
# Strings the guard refuses are built from pieces so this file never contains them literally.
# OWNER NOTE: files under .claude/hooks are changed only by the owner, by hand.
param([int]$Parallel = 8, [switch]$ShowAll)

$ErrorActionPreference = 'Stop'
$SrcDir = $PSScriptRoot
$RealRepo = (Resolve-Path (Join-Path $SrcDir '..\..')).Path.TrimEnd('\')
$PsExe = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
$Tmp = Join-Path $env:LOCALAPPDATA ('hooktest-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
$Repo = Join-Path $Tmp 'proj'
New-Item -ItemType Directory -Force -Path (Join-Path $Repo '.claude\hooks') | Out-Null
Copy-Item -LiteralPath (Join-Path $SrcDir 'guard.ps1') -Destination (Join-Path $Repo '.claude\hooks\guard.ps1')

# ---- fake secrets and names, assembled at run time ----
$DbHost = 'documents' + '.azure' + '.com'
$AcctKey = 'Account' + 'Key='
$CosmosName = 'Cosmos' + 'DbConnection' + 'String'
$Ghp = 'gh' + 'p_' + ('a1B2' * 9)
$Gho = 'gh' + 'o_' + ('a1B2' * 9)
$Ghs = 'gh' + 's_' + ('a1B2' * 9)
$KeyHdr = '-----' + 'BEGIN RSA PRIVATE KEY' + '-----'
$Ant = 'sk' + '-ant-' + ('x9Y8' * 8)
$Jwt = 'ey' + 'JhbGciOiJIUzI1NiJ9.ey' + 'JzdWIiOiIxMjM0NTY3ODkwIn0.abcDEF123456_-xyz'
$Sas = 'Shared' + 'AccessSignature sv=2020&sig=abcdef123456'
$SvSig = 'https://x.blob.core.windows.net/c?sv=2021-08-06&ss=b&sig=abcdef%3D'
$DisableHooks = 'disable' + 'AllHooks'
$LocalSettings = 'local.' + 'settings.json'
$U = 'https://bowl-madness.com/api/x'

# ---- wrapper builds (the real wrapper, built by the real builder) ----
function Get-B64([int]$ms) { [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes((& (Join-Path $SrcDir 'New-HookCommand.ps1') -SourceOnly -TimeoutMs $ms))) }
$B64Normal = Get-B64 30000
$B64Short = Get-B64 2500

# ---- case list ----
$Cases = New-Object System.Collections.Generic.List[object]
function Add-Case([string]$id, [string]$expect, [string]$tool, $ti) {
    $payload = @{ cwd = $Repo; tool_name = $tool; tool_input = $ti } | ConvertTo-Json -Depth 5 -Compress
    $rule = if ($expect -ne 'ALLOW') { $id.Split('-')[0] } else { '' }
    $Cases.Add([pscustomobject]@{ Id = $id; Group = 'rules'; Expect = $expect; Rule = $rule; B64 = $B64Normal; Payload = $payload; Root = $Repo })
}
function C([string]$id, [string]$expect, [string]$cmd, [string]$tool = 'Bash') { Add-Case $id $expect $tool @{ command = $cmd } }
function W([string]$id, [string]$expect, [string]$path, [string]$content, [string]$tool = 'Write') { Add-Case $id $expect $tool @{ file_path = $path; content = $content } }

# refuse rules, each with a near-miss that must pass
C 'R1a-delete' REFUSE 'az group delete -n foo --yes'
C 'R1b-role' REFUSE 'az role assignment create --assignee x --role Reader'
C 'R1b-near' ALLOW 'az role assignment list --assignee x'
C 'R1c-credential' REFUSE 'az ad sp credential create --id x'
C 'R2a-listkeys' REFUSE 'az rest --url /subscriptions/s/providers/x/accounts/a/listKeys?api-version=1'
C 'R2b-cosmos-keys' REFUSE 'az cosmosdb keys list -n acct -g rg'
C 'R2b-token' REFUSE 'az account get-access-token'
C 'R2b-keyvault' REFUSE 'az keyvault secret show --name s --vault-name v'
C 'R2b-keyvault-download' REFUSE 'az keyvault secret download --name s --vault-name v --file f'
C 'R2a-publishing-credentials' REFUSE 'az webapp deployment list-publishing-credentials -n x -g y'
C 'R2a-publishing-profiles' REFUSE 'az webapp deployment list-publishing-profiles -n x -g y'
C 'R2a-get-az-cosmos-key' REFUSE 'Get-AzCosmosDBAccountKey -ResourceGroupName x -Name y' PowerShell
C 'R2a-get-az-storage-key' REFUSE 'Get-AzStorageAccountKey -ResourceGroupName x -Name y' PowerShell
C 'R2a-get-az-token' REFUSE 'Get-AzAccessToken' PowerShell
C 'R2b-storage-keys' REFUSE 'az storage account keys list -n x -g y'
C 'R2b-functionapp-appsettings' REFUSE 'az functionapp config appsettings list -n x -g y'
C 'R2b-webapp-connstring' REFUSE 'az webapp config connection-string list -n x -g y'
C 'R2b-generate-sas' REFUSE 'az storage blob generate-sas -c c -n b --account-name a'
C 'R2c-reset' REFUSE 'az staticwebapp secrets reset-api-key -n x'
C 'R2d-gh-auth-token' REFUSE 'gh auth token'
C 'R2-near' ALLOW 'az cosmosdb show -n acct -g rg'
C 'R3a-acctkey' REFUSE ('echo ' + $AcctKey + 'abc')
C 'R3a-name' REFUSE ('echo ' + $CosmosName)
C 'R3a-search-near' ALLOW ('rg ' + $CosmosName + ' BowlPoolManager.Api')
C 'R3b-local-settings' REFUSE ('Get-Content BowlPoolManager.Api/' + $LocalSettings) PowerShell
C 'R3b-near' ALLOW 'Get-Content BowlPoolManager.Api/appsettings.json' PowerShell
C 'R4-post' REFUSE "curl -X POST $U"
C 'R4-delete' REFUSE "Invoke-RestMethod -Uri $U -Method Delete" PowerShell
C 'R4-data' REFUSE "curl -d '{}' $U"
C 'R4-cluster-sX' REFUSE "curl -sX POST $U"
C 'R4-cluster-sd' REFUSE "curl -sd '{}' $U"
C 'R4-cluster-sT' REFUSE "curl -sT f $U"
C 'R4-get-near' ALLOW "curl -X GET $U"
C 'R4-depth-near' ALLOW "Invoke-RestMethod $U | ConvertTo-Json -Depth 5" PowerShell
C 'R4-curl-f-near' ALLOW "curl -f $U"
C 'R4-curl-D-near' ALLOW "curl -s $U -D headers.txt"
C 'R4-curl-x-near' ALLOW "curl -x http://proxy:8080 $U"
C 'R5a-force' REFUSE 'git push --force origin main'
C 'R5a-plus' REFUSE 'git push origin +main'
C 'R5a-fu' REFUSE 'git push -fu origin x'
C 'R5a-uf' REFUSE 'git push -uf origin x'
C 'R5a-colon-plus' REFUSE 'git push origin main:+main'
C 'R5a-quoted-plus' REFUSE "git push origin '+main'"
C 'R5a-prune' REFUSE 'git push --prune origin'
C 'R5a-near' ALLOW 'git push -u origin feature-x'
# accepted false refusals: the flag rules read the whole command text, so a later -f or -F is seen
C 'R5a-later-flag-accepted' REFUSE 'git push origin config/hooks; dotnet build -f net8.0'
C 'R5a-next-statement-accepted' REFUSE 'git push origin config/hooks && powershell -NoProfile -File x.ps1' PowerShell
C 'R5a-newline-accepted' REFUSE "git push origin config/hooks`ngh pr create -F body.md"
# the same commands split across a PowerShell backtick line and a bash backslash line
$bt = '`'; $bs = '\'; $nl = "`n"
foreach ($pair in @(@('bt', $bt), @('bs', $bs))) {
    $k = $pair[0]; $c = $pair[1] + $nl
    C "R5a-continued-$k" REFUSE "git push origin ${c}--force"
    C "R5b-continued-$k" REFUSE "git reset ${c}--hard HEAD~1"
    C "R5e-continued-$k" REFUSE "git commit -m x ${c}--no-verify"
    C "A14-continued-$k" ASK "gh ${c}pr merge 5"
    C "A15-continued-$k" ASK "git push origin ${c} main"
}
C 'R5e-semicolon-in-message' REFUSE 'git commit -m "Fix a; b" --no-verify'
C 'R5e-ampersand-in-message' REFUSE 'git commit -m "Fix R&D" -n'
C 'R5e-heredoc-message' REFUSE "git commit -m `"`$(cat <<'EOF'`nFix the thing`nEOF`n)`" --no-verify"
C 'R5a-push-feature-near' ALLOW 'git push origin feature-x'
C 'R5e-commit-n' REFUSE 'git commit -n -m x'
C 'R5e-no-verify' REFUSE 'git commit -m x --no-verify'
C 'R5e-cluster' REFUSE 'git commit -an -m x'
C 'R5e-near' ALLOW 'git commit -m "add new thing"'
C 'R5e-am-near' ALLOW 'git commit -am x'
C 'R5e-amend-near' ALLOW 'git commit --amend --no-edit'
C 'R5b-reset' REFUSE 'git reset --hard HEAD~1'
C 'R5c-clean' REFUSE 'git clean -fd'
C 'R5d-reflog' REFUSE 'git reflog expire --expire=now --all'
C 'R6a-repo-delete' REFUSE 'gh repo delete owner/repo --yes'
C 'R6a-admin' REFUSE 'gh pr merge 5 --squash --admin'
C 'R6b-api-delete' REFUSE 'gh api -X DELETE repos/o/r/issues/1'
C 'R6c-remove-az' REFUSE 'Remove-AzResourceGroup -Name x' PowerShell
C 'R7a-recurse' REFUSE 'Remove-Item -Recurse -Force bin' PowerShell
C 'R7a-rmrf' REFUSE 'rm -rf obj'
C 'R7a-rd' REFUSE 'rd /s /q out' PowerShell
C 'R7a-pipe' REFUSE 'Get-ChildItem bin -Recurse | Remove-Item' PowerShell
C 'R7a-gci-ri' REFUSE 'gci foo -r | ri' PowerShell
C 'R7a-foreach' REFUSE 'Get-ChildItem -Recurse | ForEach-Object { Remove-Item $_ }' PowerShell
C 'R7a-near' ALLOW 'Remove-Item bin\foo.txt -Force' PowerShell
C 'R7a-abbrev-recurs' REFUSE 'rm -Recurs bin'
C 'R7a-abbrev-rec' REFUSE 'Remove-Item bin -Rec' PowerShell
C 'R7a-cluster-rdf' REFUSE 'rm -rdf bin'
C 'R7a-cluster-Rrf' REFUSE 'rm -Rrf bin'
C 'R7a-long-recursive' REFUSE 'rm --recursive bin'
C 'R7a-first-near' ALLOW 'Get-ChildItem x | Select-Object -First 5 | ForEach-Object { Remove-Item bin\a.txt }' PowerShell
C 'R7a-first-no-delete-near' ALLOW 'Get-ChildItem -Recurse | Select-Object -First 5' PowerShell
C 'R7a-force-near' ALLOW 'Remove-Item bin\foo.txt -Force -Verbose' PowerShell
C 'R7b-robocopy' REFUSE 'robocopy a b /MIR' PowerShell
C 'R7b-format' REFUSE 'Format-Volume -DriveLetter D' PowerShell
W 'R8a-hooks-write' REFUSE 'C:\Projects\bowl-madness\.claude\hooks\guard.ps1' 'x'
W 'R8a-settings-edit' REFUSE '.claude/settings.json' 'x' Edit
W 'R8a-git-config' REFUSE 'C:\Projects\bowl-madness\.git\config' 'x'
W 'R8a-draft-near' ALLOW 'C:\Projects\bowl-madness\.claude\hooks-draft\guard.ps1' 'x'
C 'R8b-set-content' REFUSE 'Set-Content .claude\hooks\guard.ps1 x' PowerShell
C 'R8b-checkout' REFUSE 'git checkout main -- .claude/settings.json'
C 'R8b-sc' REFUSE 'sc .claude\hooks\guard.ps1 x' PowerShell
C 'R8b-copy' REFUSE 'copy x .claude\hooks\guard.ps1' PowerShell
C 'R8b-move' REFUSE 'move x .claude\hooks\guard.ps1' PowerShell
C 'R8b-ni' REFUSE 'ni .claude/hooks/x.ps1' PowerShell
C 'R8b-new-item' REFUSE 'New-Item .claude/settings.json -Force' PowerShell
# every write form, aimed at the guard and at the settings file; not one of them is a verb the rule knows by name
foreach ($t in @('.claude\hooks\guard.ps1', '.claude\settings.json')) {
    $tn = if ($t -like '*guard*') { 'guard' } else { 'settings' }
    C "R8b-writealltext-$tn" REFUSE "[IO.File]::WriteAllText('$t','x')" PowerShell
    C "R8b-rename-item-$tn" REFUSE "Rename-Item $t x" PowerShell
    C "R8b-clear-content-$tn" REFUSE "Clear-Content $t" PowerShell
    C "R8b-robocopy-$tn" REFUSE "robocopy C:\x $t" PowerShell
    C "R8b-xcopy-$tn" REFUSE "xcopy C:\x\* $t /y" PowerShell
    C "R8b-outfile-$tn" REFUSE "Invoke-WebRequest https://example.org/y -OutFile $t" PowerShell
    C "R8b-curl-o-$tn" REFUSE "curl -o $t https://example.org/y"
    C "R8b-set-itemproperty-$tn" REFUSE "Set-ItemProperty $t IsReadOnly `$false" PowerShell
    C "R8b-git-apply-$tn" REFUSE "git apply $t.patch"
}
C 'R8b-register-hook' REFUSE 'powershell -File .claude\hooks\Register-Hook.ps1' PowerShell
C 'R8b-redirect' REFUSE 'Get-Content x > .claude\hooks\guard.ps1' PowerShell
C 'R8b-pipe' REFUSE 'Get-Content x | Set-Content .claude\hooks\guard.ps1' PowerShell
C 'R8b-two-statements' REFUSE 'git status; sc .claude\hooks\guard.ps1 x' PowerShell
C 'R8b-diff-output' REFUSE 'git diff --output=.claude/hooks/guard.ps1'
C 'R8b-restore-worktree' REFUSE 'git restore --staged --worktree .claude/hooks/guard.ps1'
C 'R8b-subexpression' REFUSE 'Get-Content (Set-Content .claude\hooks\guard.ps1 x)' PowerShell
C 'R8b-read-near' ALLOW 'Get-Content .claude\hooks\guard.ps1' PowerShell
C 'R8b-diff-near' ALLOW 'git diff -- .claude/hooks/guard.ps1'
C 'R8b-add-near' ALLOW 'git add -- .claude/hooks/guard.ps1'
C 'R8b-restore-staged' REFUSE 'git restore --staged .claude/hooks/guard.ps1'
C 'R8b-restore-staged-WS' REFUSE 'git restore --staged -WS .claude/hooks/guard.ps1'
C 'R8b-filehash-near' ALLOW 'Get-FileHash .claude\hooks\guard.ps1' PowerShell
C 'R8b-rg-near' ALLOW 'rg copy .claude/hooks'
C 'R8b-redirect-2to1-near' ALLOW 'git diff --stat -- .claude/settings.json 2>&1'
C 'R8b-draft-near' ALLOW 'Set-Content .claude\hooks-draft\guard.ps1 x' PowerShell
W 'R8c-disable' REFUSE 'docs/notes.md' ('x ' + $DisableHooks)
C 'R8d-hookspath' REFUSE 'git config core.hooksPath x'
W 'R9a-acctkey' REFUSE 'a.txt' ($AcctKey + ('A' * 44))
W 'R9a-near' ALLOW 'a.txt' ($AcctKey + 'abc')
W 'R9b-ghp' REFUSE 'a.txt' $Ghp
W 'R9b-gho' REFUSE 'a.txt' $Gho
W 'R9b-ghs' REFUSE 'a.txt' $Ghs
W 'R9c-key-header' REFUSE 'a.txt' $KeyHdr
W 'R9d-anthropic' REFUSE 'a.txt' $Ant
W 'R9e-jwt' REFUSE 'a.txt' $Jwt
W 'R9f-sas' REFUSE 'a.txt' $Sas
W 'R9f-sv-sig' REFUSE 'a.txt' $SvSig
W 'R9f-near' ALLOW 'a.txt' 'the sv=2021 version string alone'
W 'R9g-dbhost-edit' REFUSE 'a.txt' ('https://x.' + $DbHost) Edit
W 'R9-plain-words-near' ALLOW 'a.txt' 'password and ConnectionString are only words here'
# ask rules, each with a near-miss that must pass
C 'A10a-rest-post' ASK 'az rest --method post --url https://x'
C 'A10a-near' ALLOW 'az rest --method get --url https://x'
C 'A10b-field' ASK 'gh api repos/o/r/issues -f title=x'
C 'A10b-near' ALLOW 'gh api repos/o/r/issues'
C 'A10d-remove' ASK 'az extension remove -n x'
C 'A11a-checkout-dot' ASK 'git checkout .'
C 'A11a-head-dot' ASK 'git checkout HEAD -- .'
C 'A11a-near' ALLOW 'git checkout feature-x'
C 'A11b-stash-drop' ASK 'git stash drop'
C 'A14-merge' ASK 'gh pr merge 5 --squash --delete-branch'
C 'A14-global-options' ASK 'gh -R owner/repo pr merge 5'
C 'A14-near' ALLOW 'gh pr create -F body.md'
C 'A15-push-main' ASK 'git push origin main'
C 'A15-push-master' ASK 'git push origin HEAD:master'
C 'A15-git-dash-C' ASK 'git -C . push origin main'
C 'A15-near' ALLOW 'git push origin main-menu-fix'
C 'A15-next-statement-near' ALLOW 'git push origin feature-x; git checkout main'
C 'A12a-func-start' ASK 'func start' PowerShell
C 'A12b-localhost-post' ASK 'curl -X POST http://localhost:7071/api/x'
C 'A12b-cluster-sX' ASK 'curl -sX POST http://localhost:7071/api/x'
C 'A12b-near' ALLOW 'curl http://localhost:7071/api/x'
C 'A12c-swa' ASK 'swa deploy ./out' PowerShell
C 'A13-variable-url' ASK 'Invoke-RestMethod -Uri $url -Method Post' PowerShell
C 'A13-near' ALLOW 'Invoke-RestMethod -Uri $url' PowerShell
# general allow cases
C 'G-git-status' ALLOW 'git status'
C 'G-dotnet-build' ALLOW 'dotnet build BowlPoolManager.sln'
C 'G-dotnet-test' ALLOW 'dotnet test BowlPoolManager.Tests/BowlPoolManager.Tests.csproj' PowerShell
C 'G-dotnet-format' ALLOW 'dotnet format BowlPoolManager.sln --verify-no-changes' PowerShell
C 'G-live-get' ALLOW "Invoke-WebRequest $U -UseBasicParsing" PowerShell
C 'G-push-feature' ALLOW 'git push origin feature-x'
C 'G-az-group-list' ALLOW 'az group list'
C 'G-gh-pr-view' ALLOW 'gh pr view 5'
W 'G-write-source' ALLOW 'BowlPoolManager.Core/Foo.cs' 'public class Foo { }'

# ---- failure modes, through the real wrapper: every one must block ----
$good = @{ cwd = $Repo; tool_name = 'Bash'; tool_input = @{ command = 'git status' } } | ConvertTo-Json -Depth 5 -Compress
$askJson = '{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"ask","permissionDecisionReason":"stub"}}'
$allowJson = '{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"allow"}}'
function New-Root([string]$name, $guardBody) {
    $d = Join-Path $Tmp $name
    New-Item -ItemType Directory -Force -Path (Join-Path $d '.claude\hooks') | Out-Null
    if ($null -ne $guardBody) { [IO.File]::WriteAllText((Join-Path $d '.claude\hooks\guard.ps1'), $guardBody) }
    return $d
}
function Add-Fail([string]$id, [string]$expect, $root, [string]$payload, [string]$b64 = $B64Normal) {
    $Cases.Add([pscustomobject]@{ Id = $id; Group = 'failure'; Expect = $expect; Rule = ''; B64 = $b64; Payload = $payload; Root = $root })
}
Add-Fail 'F-empty-input' REFUSE $Repo ''
Add-Fail 'F-malformed-json' REFUSE $Repo '{"tool_name": "Bash", "tool_input": {'
Add-Fail 'F-not-json' REFUSE $Repo 'git status'
Add-Fail 'F-no-tool-input' REFUSE $Repo '{"tool_name":"Bash"}'
Add-Fail 'F-empty-command' REFUSE $Repo '{"tool_name":"Bash","tool_input":{"command":""}}'
Add-Fail 'F-unknown-tool' REFUSE $Repo '{"tool_name":"Mystery","tool_input":{"x":1}}'
Add-Fail 'F-write-no-path' REFUSE $Repo '{"tool_name":"Write","tool_input":{"content":"x"}}'
Add-Fail 'F-script-missing' REFUSE (New-Root 'missing' $null) $good
Add-Fail 'F-script-throws' REFUSE (New-Root 'throw' "throw 'boom from inside the guard'") $good
Add-Fail 'F-script-syntax-error' REFUSE (New-Root 'syntax' 'if ( {{{ not valid powershell') $good
Add-Fail 'F-script-garbage-output' REFUSE (New-Root 'garbage' "Write-Output 'hello'; exit 0") $good
Add-Fail 'F-script-empty-output' REFUSE (New-Root 'silent' 'exit 0') $good
Add-Fail 'F-script-empty-file' REFUSE (New-Root 'empty' '') $good
Add-Fail 'F-script-hangs' REFUSE (New-Root 'hang' 'Start-Sleep -Seconds 120') $good $B64Short
Add-Fail 'F-script-allow-json' REFUSE (New-Root 'allowjson' "[Console]::Out.Write('$allowJson'); exit 0") $good
Add-Fail 'F-script-ok-marker' ALLOW (New-Root 'okmarker' "[Console]::Out.Write('guard-ok'); exit 0") $good
Add-Fail 'F-script-ask-passthrough' ASK (New-Root 'askpass' "[Console]::Out.Write('$askJson'); exit 0") $good

# ---- run ----
function Start-Hook($c) {
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $PsExe
    $psi.Arguments = '-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand ' + $c.B64
    $psi.UseShellExecute = $false
    $psi.RedirectStandardInput = $true; $psi.RedirectStandardOutput = $true; $psi.RedirectStandardError = $true
    $psi.StandardOutputEncoding = [Text.Encoding]::UTF8; $psi.StandardErrorEncoding = [Text.Encoding]::UTF8
    $psi.CreateNoWindow = $true
    $psi.WorkingDirectory = $Repo
    $psi.EnvironmentVariables['CLAUDE_PROJECT_DIR'] = $c.Root
    $p = [Diagnostics.Process]::Start($psi)
    $o = $p.StandardOutput.ReadToEndAsync(); $e = $p.StandardError.ReadToEndAsync()
    $bytes = (New-Object Text.UTF8Encoding($false)).GetBytes($c.Payload)
    try { $p.StandardInput.BaseStream.Write($bytes, 0, $bytes.Length); $p.StandardInput.Close() } catch { }
    return [pscustomobject]@{ P = $p; O = $o; E = $e }
}
function Get-Outcome($h, $case) {
    if (-not $h.P.WaitForExit(40000)) { try { $h.P.Kill() } catch { }; return 'HARNESS TIMEOUT' }
    $h.P.WaitForExit()
    $out = $h.O.Result.Trim(); $err = $h.E.Result.Trim()
    $code = $h.P.ExitCode
    $o = if ($code -eq 2) { 'REFUSE' } elseif ($code -eq 0 -and $out -eq '') { 'ALLOW' } elseif ($code -eq 0 -and $out -match '"permissionDecision":"ask"') { 'ASK' } else { "OTHER(exit $code)" }
    if ($case.Rule -and $o -eq $case.Expect) {
        $text = if ($o -eq 'REFUSE') { $err } else { $out }
        if ($text -notmatch ('(?:: |\| )' + $case.Rule + ' ')) { return "$o but not $($case.Rule)" }
    }
    return $o
}
$results = New-Object System.Collections.Generic.List[object]
for ($i = 0; $i -lt $Cases.Count; $i += $Parallel) {
    $batch = @($Cases[$i..([Math]::Min($i + $Parallel, $Cases.Count) - 1)])
    $hooks = @($batch | ForEach-Object { Start-Hook $_ })
    for ($k = 0; $k -lt $batch.Count; $k++) {
        $actual = Get-Outcome $hooks[$k] $batch[$k]
        $results.Add([pscustomobject]@{ Case = $batch[$k].Id; Group = $batch[$k].Group; Expected = $batch[$k].Expect; Actual = $actual; Result = $(if ($actual -eq $batch[$k].Expect) { 'PASS' } else { 'FAIL' }) })
    }
}

# ---- Register-Hook.ps1 run exactly as the owner runs it: relative -File path, project root as the folder, no -Settings ----
$Reg = Join-Path $Tmp 'regproj'
New-Item -ItemType Directory -Force -Path (Join-Path $Reg '.claude\hooks') | Out-Null
foreach ($f in 'Register-Hook.ps1', 'New-HookCommand.ps1', 'wrapper.src.ps1', 'guard.ps1', 'test-guard.ps1') { Copy-Item -LiteralPath (Join-Path $SrcDir $f) -Destination (Join-Path $Reg ".claude\hooks\$f") }
$RegSettings = Join-Path $Reg '.claude\settings.json'
[IO.File]::WriteAllText($RegSettings, '{"hooks":{"PreToolUse":[{"matcher":"Bash","hooks":[{"type":"command","command":"powershell.exe -EncodedCommand AAAA","timeout":10}]}]},"permissions":{"allow":[]}}')
$regBefore = (Get-FileHash -LiteralPath $RegSettings).Hash
$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = $PsExe
$psi.Arguments = '-NoProfile -File .claude\hooks\Register-Hook.ps1 -WhatIf'
$psi.WorkingDirectory = $Reg
$psi.UseShellExecute = $false; $psi.RedirectStandardOutput = $true; $psi.RedirectStandardError = $true; $psi.CreateNoWindow = $true
$p = [Diagnostics.Process]::Start($psi)
$ro = $p.StandardOutput.ReadToEndAsync(); $re = $p.StandardError.ReadToEndAsync()
$null = $p.WaitForExit(60000); $p.WaitForExit()
$regOk = ($p.ExitCode -eq 0) -and ($ro.Result -match 'matcher:') -and ($re.Result.Trim() -eq '') -and ((Get-FileHash -LiteralPath $RegSettings).Hash -eq $regBefore) -and -not (Test-Path -LiteralPath ($RegSettings + '.bak')) -and -not (Test-Path -LiteralPath ($RegSettings + '.tmp'))
$results.Add([pscustomobject]@{ Case = 'RH-whatif-relative-file'; Group = 'register'; Expected = 'exit 0, nothing changed'; Actual = $(if ($regOk) { 'exit 0, nothing changed' } else { "exit $($p.ExitCode): " + $re.Result.Trim() }); Result = $(if ($regOk) { 'PASS' } else { 'FAIL' }) })

# ---- settings and self-check ----
$settings = Get-Content -LiteralPath (Join-Path $RealRepo '.claude\settings.json') -Raw | ConvertFrom-Json
$hasRead = (@($settings.permissions.deny) -contains ('Read(**/' + $LocalSettings + ')'))
$results.Add([pscustomobject]@{ Case = 'S-read-deny'; Group = 'settings'; Expected = 'present'; Actual = $(if ($hasRead) { 'present' } else { 'missing' }); Result = $(if ($hasRead) { 'PASS' } else { 'FAIL' }) })
$bad = @()
foreach ($f in Get-ChildItem -LiteralPath $SrcDir -File -Filter '*.ps1') {
    $t = Get-Content -LiteralPath $f.FullName -Raw
    foreach ($lit in @($DbHost, $AcctKey, $Ghp.Substring(0, 4), $Gho.Substring(0, 4), $Ant.Substring(0, 7))) { if ($t.Contains($lit)) { $bad += "$($f.Name) has '$lit'" } }
}
$results.Add([pscustomobject]@{ Case = 'S-no-literals'; Group = 'settings'; Expected = 'none'; Actual = $(if ($bad.Count -eq 0) { 'none' } else { $bad -join '; ' }); Result = $(if ($bad.Count -eq 0) { 'PASS' } else { 'FAIL' }) })

Remove-Item -LiteralPath $Tmp -Recurse -Force -ErrorAction SilentlyContinue

# ---- report ----
if ($ShowAll) { $results | Format-Table -AutoSize } else { $results | Where-Object { $_.Result -eq 'FAIL' } | Format-Table -AutoSize }
Write-Host ('Summary  ' + (($results | Group-Object Group | ForEach-Object { '{0}: {1}/{2}' -f $_.Name, @($_.Group | Where-Object { $_.Result -eq 'PASS' }).Count, $_.Count }) -join '   '))
$failed = @($results | Where-Object { $_.Result -eq 'FAIL' })
Write-Host ('Total {0}, failed {1}' -f $results.Count, $failed.Count)
exit $(if ($failed.Count -eq 0) { 0 } else { 1 })
