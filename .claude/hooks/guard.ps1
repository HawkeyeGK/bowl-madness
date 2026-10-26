<#
  PreToolUse guard for Bash, PowerShell, Monitor, mcp__terminal__run_in_terminal (commands)
  and Write, Edit, MultiEdit, NotebookEdit (file writes).

  OWNER NOTE: files under .claude/hooks are changed only by the owner, by hand.

  How it works: one table of rules ($Rules below). Each row is
      id, decision (refuse or ask), scope, regex, message
  Scope is sh (the command text), path (file_path of a write tool) or body (the new text of a
  write tool). The loop at the bottom applies every row of that scope to the whole text, as typed
  (matching is case-insensitive; the curl flag letters are the one case-sensitive part). There is
  no parsing of any kind. Rules match the whole command text. Only A14 and A15 use ~ in their
  pattern, meaning "anything up to the next ; | & or newline (a backtick or backslash line
  continuation does not end it)", so there the words have to sit in the same statement.

  Contract with wrapper.src.ps1 (the registered hook command, unchanged):
    refuse          : reason on stderr, exit 2
    ask             : PreToolUse JSON with permissionDecision "ask" on stdout, exit 0
    no objection    : prints the marker guard-ok, exit 0 (the wrapper turns that into silence)
    any exception, empty or unparseable input, unknown tool : exit 2

  Refused strings (database host, key names, token prefixes) are built from pieces so this file
  never contains them literally.

  KNOWN LIMITS. A guardrail against careless commands, not a sandbox. Each of these is accepted:
    - Disguised commands: variables, string building, aliases, backticks, -EncodedCommand,
      8.3 short names, anything that needs the text to be parsed or run to be understood.
    - Script files are never read; python, node and dotnet HTTP clients, Start-Process.
    - Very long commands (about 20 KB or more), or hundreds of git/push pairs, can hit the
      3 second regex timeout and are refused. A large file write can be refused the same way.
    - PowerShell parameter abbreviations (irm -Me Post) and short options glued to a value (curl -dfoo).
    - The search exemption in R3a applies only when the search tool (rg, grep, git grep,
      Select-String, findstr) is the first word; "cd x; rg ..." or "gci | sls ..." is refused.
    - R8b lets a protected path through only in one read-only statement (see $readOnly). Any
      other command that names one, even a harmless cd, a pipe, a ; chain or a commit message, is
      refused. A path split across commands (cd .claude; sc settings.json x) is not seen. The
      read tools are checked for --output and --pre only; rg --hostname-bin is not.
    - Flag rules (R1, R2, R5, R6, R7, A10, A11) read the whole command text, so a flag later in a
      compound command ("git push origin x; dotnet build -f net8.0"), or inside quoted text (a commit
      message that says "git reset --hard"), is refused too. -Dir is read as a recursion flag by R7a.
    - A plain git push with no branch named (while on main) is not seen, and neither are
      git push --all or --no-verify; R5e covers git commit only.
    - Not in the table: terraform, azd, GitHub deletes outside R6 (issue, run, label, cache, gist,
      ssh-key, secret remove), az keyvault secret list, other Az module cmdlets than
      Remove-Az* and the three Get-Az key and token cmdlets, other spellings of localhost
      (0.0.0.0, 127.1), git checkout -f, func.exe, Clear-Disk, .claude/agents and .claude/commands.
    - Write checks the new text only. Grep, Glob and Read are not in the matcher.
#>
$ErrorActionPreference = 'Stop'
$opts = [Text.RegularExpressions.RegexOptions]'IgnoreCase, Singleline'
$limit = [TimeSpan]::FromSeconds(3)   # a regex that runs away throws, and a throw blocks

# ---------- pieces ----------
$S = '(?:[^;|&\r\n]|[`\\]\r?\n)*'   # what ~ stands for in A14 and A15
$web = '\b(?:curl|wget|iwr|irm|invoke-webrequest|invoke-restmethod)\b'
$live = 'bowl-madness\.com|hoops-madness\.com|polite-glacier-059273d10'
$local = 'localhost|127\.0\.0\.1|\[::1\]'
# curl letters are case-sensitive (-X -d -F -T send data; -f -D -x do not), alone or in a cluster like -sX
$nonget = '(?-i:\s-[a-zA-Z]{0,4}X\s*(?!GET\b)[A-Za-z]|\s-[a-zA-Z]{0,4}[dFT](?![a-zA-Z]))|\s--?(?:request|method)[\s:=]+["'']?(?!get\b)[a-z]|\s--(?:data|form|json|upload-file|post-)|\s-(?:body|infile|form)\b'
$hookPath = '\.claude[\\/]hooks(?![\w-])|\.git[\\/]hooks|\.claude[\\/]settings(?:\.local)?\.json|\.git[\\/]config'
# the only shape allowed to name a protected path: ONE statement (no ; & | > ( ) { } backtick or newline,
# apart from 2>&1) that starts with a read-only tool, with no flag that makes the tool write or run something
$readOnly = '\s*(?:git\s+(?:diff|show|log|status|ls-files|add)|get-filehash|get-content|cat|type|rg|grep|select-string|get-childitem|ls|dir|test-path)\b(?!.*(?:--output|--pre\b))(?:2>&1|[^;&|>(){}`\r\n])*\s*\z'
$recurse = '\s-(?:rec[a-z]*|[fidv]*r[fidvr]*)(?![a-z])|\s--recursive\b|\s/s\b'   # -r -rf -rdf -Rrf -Recurs..., not -First
$delete = '\b(?:remove-item|ri|rm|rd|rmdir|del|erase)\b'
$dbHost = 'documents' + '\.azure' + '\.com'
$acctKey = 'account' + 'key='
$cosmosName = 'cosmos' + 'dbconnection' + 'string'
$noDisable = 'disable' + 'allhooks'
function All { '^' + (($args | ForEach-Object { '(?=.*(?:' + $_ + '))' }) -join '') }   # every piece must appear anywhere

# ---------- rules ----------
$Rules = @(
    @('R1a', 'refuse', 'sh', '\baz\b.*\b(?:delete|purge)\b', 'az delete or purge.'),
    @('R1b', 'refuse', 'sh', '\baz\b.*\brole\s+(?:assignment|definition)\b.*\b(?:create|update|delete)\b', 'az role assignment or definition change.'),
    @('R1c', 'refuse', 'sh', '\baz\b.*\bad\b.*\bcredential\s+(?:reset|create|update|delete)\b', 'az ad credential change.'),
    @('R2a', 'refuse', 'sh', 'listkeys|listconnectionstrings|listcredentials|listsecrets|listsastokens|regeneratekey|\blist-publishing-(?:credentials|profiles)\b|\bget-az(?:cosmosdbaccountkey|storageaccountkey|accesstoken)\b', 'key, secret, publishing credential and token reads.'),
    @('R2b', 'refuse', 'sh', '\baz\b.*(?:\bcosmosdb\s+keys\b|\blist-keys\b|\blist-read-only-keys\b|\blist-connection-strings\b|\baccount\s+get-access-token\b|\bstaticwebapp\s+(?:secrets|appsettings)\s+list\b|\bfunctionapp\b.*\bkeys\s+list\b|\bkeyvault\s+secret\s+(?:show|download)\b|\bstorage\s+account\s+keys\b|\b(?:functionapp|webapp)\b.*\bconfig\s+(?:appsettings|connection-string)\s+list\b|\bgenerate-sas\b)', 'az command that reads keys, secrets, app settings or tokens.'),
    @('R2c', 'refuse', 'sh', '\baz\b.*\b(?:regenerate|renew|rotate|reset)\b', 'az command that regenerates, renews, rotates or resets keys.'),
    @('R2d', 'refuse', 'sh', '\bgh\s+auth\s+token\b|--show-token', 'prints the GitHub token.'),
    @('R3a', 'refuse', 'sh', ('^(?!\s*(?:rg|grep|git\s+grep|select-string|findstr)\b).*(?:' + $dbHost + '|' + $acctKey + '|' + $cosmosName + ')'), 'database host, account key or connection string name outside a plain search.'),
    @('R3b', 'refuse', 'sh', 'local\.sett', 'local settings file named in a shell command.'),
    @('R4', 'refuse', 'sh', (All $web $live $nonget), 'non-GET web request to the live site.'),
    @('R5a', 'refuse', 'sh', '\bgit\b.*\bpush\b.*(?:\s--(?:force|mirror|delete|prune)\b|\s-[a-z]*[fd][a-z]*\b|\s["'']?\+\S|\s\S*:\+|\s["'']?:[\w./-])', 'git push that forces, mirrors, prunes, deletes or uses a + or : refspec.'),
    @('R5b', 'refuse', 'sh', '\bgit\b.*\breset\b.*--hard', 'git reset --hard.'),
    @('R5c', 'refuse', 'sh', '\bgit\b.*\bclean\b.*(?:\s-[a-z]*f|\s--force)', 'git clean with force.'),
    @('R5d', 'refuse', 'sh', '\bgit\b.*(?:\b(?:filter-branch|filter-repo)\b|\breflog\s+expire\b|\bgc\b.*--prune|\bupdate-ref\b.*\s-d\b)', 'git history rewrite or object pruning.'),
    @('R5e', 'refuse', 'sh', '\bgit\b.*\bcommit\b.*(?:\s--no-verify\b|\s-[a-z]*n[a-z]*\b)', 'git commit that skips the commit hooks (-n or --no-verify).'),
    @('R6a', 'refuse', 'sh', '\bgh\b.*(?:\brepo\s+(?:delete|archive|rename|transfer)\b|\brepo\s+edit\b.*--visibility|\brelease\s+delete\b|\b(?:secret|variable)\s+(?:set|delete)\b|\bworkflow\s+disable\b|\bpr\s+merge\b.*--admin)', 'destructive or settings-changing gh command.'),
    @('R6b', 'refuse', 'sh', '\bgh\b.*\bapi\b.*(?:\s-x\s*["'']?delete\b|--method[\s=]+["'']?delete\b)', 'gh api DELETE.'),
    @('R6c', 'refuse', 'sh', '\bremove-az\w*', 'Remove-Az cmdlet.'),
    @('R7a', 'refuse', 'sh', (All $recurse $delete), 'recursive delete, also through a pipe. Use dotnet clean or run it yourself.'),
    @('R7b', 'refuse', 'sh', '\b(?:format-volume|diskpart)\b|\bformat\s+[a-z]:(?!\S)|\brobocopy\b.*\s/(?:mir|purge)\b', 'disk format or mirror-delete copy.'),
    @('R8a', 'refuse', 'path', '(?:^|[\\/])\.claude[\\/]hooks(?:[\\/]|$)|(?:^|[\\/])\.git[\\/]hooks(?:[\\/]|$)|(?:^|[\\/])\.claude[\\/]settings(?:\.local)?\.json$|(?:^|[\\/])\.git[\\/]config$', 'hooks, settings files and .git/config are changed only by the owner, by hand.'),
    @('R8b', 'refuse', 'sh', ('^(?=.*(?:' + $hookPath + '))(?!' + $readOnly + ')'), 'shell command names hooks, a settings file or .git/config and is not a single read-only statement. Run it yourself.'),
    @('R8c', 'refuse', 'sh,body', $noDisable, 'turning the hooks off is not allowed.'),
    @('R8d', 'refuse', 'sh', 'core\.hookspath', 'redirecting git hooks is for the owner.'),
    @('R9a', 'refuse', 'body', ($acctKey + '[a-z0-9+/]{40,}'), 'content holds a storage account key.'),
    @('R9b', 'refuse', 'body', ('\bgh' + '[pousr]_[a-z0-9]{30,}|github' + '_pat_[a-z0-9_]{30,}'), 'content holds a GitHub token.'),
    @('R9c', 'refuse', 'body', ('-----begin [a-z ]*private key' + '-----'), 'content holds a private key.'),
    @('R9d', 'refuse', 'body', ('sk' + '-ant-[a-z0-9_-]{20,}'), 'content holds an Anthropic key.'),
    @('R9e', 'refuse', 'body', ('ey' + 'j[a-z0-9_-]{10,}\.ey' + 'j[a-z0-9_-]{10,}'), 'content holds a JWT.'),
    @('R9f', 'refuse', 'body', ('sharedaccess' + 'signature.{0,300}sig=|\bsv=\d{4}-\d{2}-\d{2}.{0,300}\bsig='), 'content holds a shared access signature.'),
    @('R9g', 'refuse', 'body', $dbHost, 'content holds the database host name.'),
    @('A10a', 'ask', 'sh', '\baz\b.*\brest\b.*\s(?:--method|-m)[\s=]+["'']?(?!get\b)[a-z]', 'az rest with a non-GET method.'),
    @('A10b', 'ask', 'sh', '\bgh\b.*\bapi\b.*(?:\s-x\s*["'']?(?!get\b)[a-z]|--method[\s=]+["'']?(?!get\b)[a-z]|\s-f(?![a-z])|\s--input\b|\s--(?:raw-)?field\b)', 'gh api with a non-GET method or a body.'),
    @('A10c', 'ask', 'sh', '\baz\b.*\bdeployment\b.*\b(?:create|cancel)\b', 'az deployment create or cancel.'),
    @('A10d', 'ask', 'sh', '\baz\b.*\bremove\b', 'az remove.'),
    @('A11a', 'ask', 'sh', '\bgit\b.*\b(?:checkout|restore)\s+(?:\S+\s+)?(?:--\s+)?\.(?![\w./\\-])', 'discards every local change.'),
    @('A11b', 'ask', 'sh', '\bgit\b.*\bstash\s+(?:drop|clear)\b', 'drops stashed work.'),
    @('A12a', 'ask', 'sh', '\bfunc\s+(?:host\s+)?start\b', 'starts the local Functions host.'),
    @('A12b', 'ask', 'sh', (All $web $local $nonget), 'non-GET web request to localhost.'),
    @('A12c', 'ask', 'sh', '\bfunc\s+azure\s+functionapp\s+publish\b|\bswa\s+deploy\b', 'deploys to Azure.'),
    @('A13', 'ask', 'sh', ('^(?=.*(?:' + $web + '))(?=.*(?:' + $nonget + '))(?=.*\$)(?!.*https?://[a-z0-9])'), 'non-GET web request where the URL is a variable or expression.'),
    @('A14', 'ask', 'sh', '\bgh\b~\bpr(?:\s|[`\\]\r?\n)+merge\b', 'merging a pull request (a merge to main deploys to production).'),
    @('A15', 'ask', 'sh', '\bgit\b~\bpush\b~[\s:/''"](?:main|master)(?![\w.-])', 'git push that names main or master (a push to main deploys to production).')
)
foreach ($r in $Rules) { if ($r[0] -in 'A14', 'A15') { $r[3] = $r[3].Replace('~', $S) } }

# ---------- main ----------
function Invoke-Main {
    $ms = New-Object System.IO.MemoryStream
    [Console]::OpenStandardInput().CopyTo($ms)
    $txt = [Text.Encoding]::UTF8.GetString($ms.ToArray()).TrimStart([char]0xFEFF)
    if ([string]::IsNullOrWhiteSpace($txt)) { throw 'empty hook input' }
    $in = $txt | ConvertFrom-Json
    $tool = [string]$in.tool_name
    $ti = $in.tool_input
    if (-not $tool -or $null -eq $ti) { throw 'hook input has no tool_name or tool_input' }

    if ($tool -in 'Bash', 'PowerShell', 'Monitor', 'mcp__terminal__run_in_terminal') {
        $cmd = [string]$ti.command
        if ([string]::IsNullOrWhiteSpace($cmd) -and $tool -eq 'Monitor') { $cmd = [string]$ti.ws.url }
        if ([string]::IsNullOrWhiteSpace($cmd)) { throw 'shell command is empty' }
        $subject = @{ sh = $cmd }
    }
    elseif ($tool -in 'Write', 'Edit', 'MultiEdit', 'NotebookEdit') {
        $path = [string]$ti.file_path
        if ([string]::IsNullOrWhiteSpace($path)) { $path = [string]$ti.notebook_path }
        if ([string]::IsNullOrWhiteSpace($path)) { throw 'write has no file path' }
        $parts = @($ti.content, $ti.new_string, $ti.new_source) + @($ti.edits | ForEach-Object { $_.new_string })
        $subject = @{ path = $path; body = (($parts | Where-Object { $_ }) -join "`n") }
    }
    else { throw "tool '$tool' is not handled by this guard" }

    $refuse = @(); $ask = @()
    foreach ($r in $Rules) {
        foreach ($s in ($r[2] -split ',')) {
            if ($subject.ContainsKey($s) -and [regex]::IsMatch($subject[$s], $r[3], $opts, $limit)) {
                if ($r[1] -eq 'refuse') { $refuse += "$($r[0]) $($r[4])" } else { $ask += "$($r[0]) $($r[4])" }
            }
        }
    }
    if ($refuse.Count -gt 0) {
        [Console]::Error.WriteLine('BLOCKED by repo hook (no override): ' + ($refuse -join ' | ') + ' If you really intend this, run it yourself in your own terminal.')
        exit 2
    }
    if ($ask.Count -gt 0) {
        $obj = @{ hookSpecificOutput = @{ hookEventName = 'PreToolUse'; permissionDecision = 'ask'; permissionDecisionReason = 'Repo hook asks for confirmation: ' + ($ask -join ' | ') } }
        $bytes = (New-Object System.Text.UTF8Encoding($false)).GetBytes(($obj | ConvertTo-Json -Compress -Depth 5))
        $so = [Console]::OpenStandardOutput(); $so.Write($bytes, 0, $bytes.Length); $so.Flush()
        exit 0
    }
    [Console]::Out.Write('guard-ok')
    exit 0
}

try { Invoke-Main }
catch {
    [Console]::Error.WriteLine('BLOCKED by repo hook (guard failed closed): ' + $_.Exception.Message)
    exit 2
}
