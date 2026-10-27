# CLAUDE.md

Guidance for Claude Code in this repository. Architecture detail is in
ARCHITECTURAL_CONTEXT.md. Intended behavior is in docs/BUSINESS-RULES.md.

## Environment
Windows with PowerShell. Use PowerShell syntax, not Bash.

    dotnet build BowlPoolManager.sln
    dotnet test BowlPoolManager.Tests/BowlPoolManager.Tests.csproj
    dotnet test BowlPoolManager.Tests/BowlPoolManager.Tests.csproj --filter "FullyQualifiedName~ScoringEngineTests"
    cd BowlPoolManager.Api; func start        # needs Azure Functions Core Tools
    cd BowlPoolManager.Client; dotnet run

| Project | Target | Role |
|---|---|---|
| BowlPoolManager.Core | net8.0 | Shared models, DTOs, scoring engines, constants |
| BowlPoolManager.Api | net8.0 | Azure Functions v4 isolated, REST API |
| BowlPoolManager.Client | net10.0 | Blazor WebAssembly |
| BowlPoolManager.Tests | net10.0 | xUnit tests for Core and Api |

Hosting is Azure Static Web Apps. A push to main deploys to production.

## Workflow
- Read the code you will change and its callers first. Say what you found.
- Change only what the task needs.
- If you find something outside the task, stop and tell me. I decide whether to fix it
  in this session or file a GitHub Issue. Draft the issue for me to approve.
- Decide implementation details yourself and explain the choice in your summary: naming,
  syntax, code structure inside a file, how to use a library.
- Ask me before decisions that shape the system or what users see: architecture and
  structure, new patterns, new dependencies or technologies, data model changes, and UI
  and UX choices such as icons, labels, layout and flow. Give two or three options, your
  recommendation, and the tradeoffs in plain language.
- Ask me about intended behavior when docs/BUSINESS-RULES.md doesn't answer it.

## Git
- Code changes go on a branch with a pull request. Merge only when I say so, using
  gh pr merge --squash --delete-branch, and ask before running it. Before asking to merge,
  run gh pr checks and tell me the result and the preview address. If any check failed,
  don't merge. A merge to main deploys to production. Never push code to main.
- Changes that only add or edit documents may be committed straight to main. Ask before pushing.
- Don't commit until I say the change is what I want, or I ask for a checkpoint.
  Group related changes into one commit unless I say otherwise.
- Reference the issue in the commit message when one exists (Closes #N or Refs #N).
- Stage files by name. Do not use git add . or git add -A.
- Before opening a PR or pushing:
  1. Build and test. No new failures beyond the failures already tracked in open issues.
  2. Run the code-review agent. Also run security-data-review when the change touches
     authorization, live-data writes, scoring or permissions. Fix critical and high
     findings. Stop after about three rounds and report what is unresolved in plain language.
  3. Give me a summary: what changed, why, what could go wrong, what was tested.
- Ask before git push and again before gh pr create.
- After opening or updating a PR, wait for its checks before reporting anything. GitHub takes
  a few seconds to start them, so "no checks reported" means not started, not passed. Poll
  gh pr checks until checks appear (give up after about three minutes), then watch them until
  they finish. Report only the final result. Never ask to merge before every check has
  finished and passed.
- After a PR is merged (I say so, or you merged it):
  1. Run gh pr view <number> --json state,headRefOid,mergeCommit.
  2. Confirm state is MERGED and the local branch tip equals headRefOid. If either
     fails, stop and tell me.
  3. Switch to main and pull with --ff-only.
  4. gh pr merge --delete-branch may already have deleted the local branch. If it still
     exists, delete it with git branch -D. Stage and commit nothing.
  5. Show git status.

## Bugs
- Write a failing test first. Run the tests on unchanged code to get a baseline.
- Use a git worktree for the baseline, not git stash.

## Business rules
- docs/BUSINESS-RULES.md is the intended behavior. Where code disagrees, the code is the bug.
- Read the relevant section before changing pool, entry, scoring or permission code.
- Before changing code in an area it covers, check open GitHub issues and their comments for that area.
- If code deviates from the rules and no issue exists, report it. Do not fix it silently.
- Don't edit docs/BUSINESS-RULES.md unless I ask. Propose changes and I decide.

## Docs
When a change alters architecture, the data model, infrastructure or business rules, propose
the matching doc edit in the same change. I approve doc edits. One home per fact: rules in
CLAUDE.md and BUSINESS-RULES, architecture in ARCHITECTURAL_CONTEXT, status in GitHub Issues.

## Data (Cosmos DB, database BowlMadnessDb)
| Container | Partition key | Holds |
|---|---|---|
| Players | /id | User profiles |
| Seasons | /seasonId | Seasons, bowl and hoops pools, bowl and hoops games |
| Picks | /seasonId | Bowl entries, hoops entries (HoopsBracketEntry) |
| PoolArchives | /seasonId | Archives (ids Archive_{poolId}, HoopsArchive_{poolId}) |
| Configuration | /id | Team lists, API integration settings |

- Pass the partition key on every query and write.
- Some hoops pools and entries use the text "2026" as seasonId, not the season GUID.
  See BUSINESS-RULES section 6 before touching season ids.
- Dry-run any bulk or destructive change, report the counts, and wait for my approval.
  Use /data-change for any bulk or destructive data operation.
- Never write to live data, or send non-GET requests to the live site, without my explicit approval.
  Before a bulk or destructive data change, confirm the production Cosmos account is on
  continuous backup (az cosmosdb show with --query backupPolicy) and tell me the UTC time the
  change starts, which is the restore point. Do not start the change if the policy is not Continuous.

## Security
- Every function that writes data checks the caller first, on the server, before any other
  logic. Hiding a button is not a protection.
- No secrets in code, logs or docs. The Cosmos connection string stays in app settings.
- Error responses may include exception messages, stack traces and internal ids. They must never include secrets, keys, connection strings, tokens, or other players' picks before lock.

## Tests
- xUnit, FluentAssertions, Moq. Name tests MethodName_ShouldExpectedBehavior_WhenCondition.
- Use It.IsAny<T>() in Moq setups.
- New logic gets tests. Bug fixes get a regression test. Tests never touch production data.

## Dates and models
- Store UTC, display Central. Use DateTime.UtcNow, never DateTime.Now.
- Models stored in Cosmos carry both [JsonProperty] and [JsonPropertyName] on every property.
- Models shared by Api and Client live in Core.

## Common AI mistakes here
- Changing error handling nobody asked about.
- A Cosmos call without a partition key.
- Removing a security check during a refactor.
- Editing files outside the task.
- Calling methods that do not exist. Check they exist before using them.

## Never
- Force-push, git reset --hard, or skip commit checks (--no-verify).
- Push code to main.
- Delete Azure resources.
- Read .env files.

## Hooks

A PreToolUse hook (`.claude/hooks/guard.ps1`, one table of rules read against the command text) stops these before they run:

- Refused: az delete or purge, role and credential changes, reading or rotating keys, secrets and tokens; the database host, account key or connection string name in a command (plain searches excepted); any shell command naming the local settings file; non-GET requests to the live site; force pushes, `reset --hard`, `clean -f` and other history-destroying git; destructive gh commands and `gh pr merge --admin`; recursive deletes (use `dotnet clean`); secrets in file writes; `git commit -n` or `--no-verify`; any edit to `.claude/hooks`, `.git/hooks`, `.git/config` or a settings file, any shell command that names those paths unless it is one read-only statement (`git diff`, `git add`, `rg`, `Get-Content` and similar), and any attempt to turn the hooks off.
- Ask: non-GET `az rest` or `gh api`, `az ... remove`, `git checkout .`, `git stash drop`, `func start`, non-GET requests to localhost, `func azure functionapp publish`, `swa deploy`, a `git push` that names main or master, and any `gh pr merge`. The `gh pr merge` ask rule in the settings stays too; run `gh pr checks` first, as the Git section says.

- When a command is refused, the message names the rule. Report the rule to the owner. If the task can be done another way that does not need the blocked action, do that. Never rewrite the same blocked action in another spelling or form to get past a hook, and never try to disable, edit or work around a hook.
- Hooks and settings files are changed only by the owner, by hand. To propose a rule change, put a draft in `.claude/hooks-draft/` and say which files to copy; the owner copies them in and runs `Register-Hook.ps1`.
- The hook is a guardrail against careless commands, not a sandbox. It does not parse commands, so disguised forms get through. The known limits are in `guard.ps1`.
