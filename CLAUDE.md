# CLAUDE.md

Guidance for Claude Code in this repository. Architecture detail is in
ARCHITECTURAL_CONTEXT.md. Intended behavior is in docs/BUSINESS-RULES.md.

## Environment
Windows with PowerShell. Use PowerShell syntax, not Bash.

    dotnet build BowlPoolManager.sln
    dotnet test BowlPoolManager.Tests/BowlPoolManager.Tests.csproj
    dotnet test BowlPoolManager.Tests/BowlPoolManager.Tests.csproj --filter "FullyQualifiedName~ScoringEngineTests"

Local runs have no database: the local settings hold a placeholder and no test database exists. func start asks for approval.
Projects, roles and target frameworks are in ARCHITECTURAL_CONTEXT.md sections 1 and 9.

Hosting is Azure Static Web Apps. A merge or push to main deploys to production.

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
- When troubleshooting a failure, read 'Reading the logs' in docs/INFRASTRUCTURE.md first.

## Git
- Code changes go on a branch with a pull request. Merge only when I say so, using
  gh pr merge --squash --delete-branch, and ask before running it. Before asking to merge,
  run gh pr checks and tell me the result and the preview address. If any check failed,
  don't merge. Never push code to main.
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
- After opening or updating a PR, wait for its checks to finish before reporting. "No checks
  reported" means not started, not passed. Never ask to merge before every check has finished
  and passed.
- After a PR is merged (I say so, or you merged it): verify the PR is MERGED and the local tip
  matches its head (gh pr view <number> --json state,headRefOid); if either fails, stop and
  tell me. Switch to main (if not already on it) and pull with --ff-only. Delete the local
  branch only if it still exists. Show git status. Stage and commit nothing.

## Bugs
- Write a failing test first.
- Run the tests on the unchanged code before editing, without stashing my work.

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
- Pass the partition key on every query and write. Container partition keys are in
  ARCHITECTURAL_CONTEXT.md section 2.
- Some hoops pools and entries use the text "2026" as seasonId, not the season GUID.
  See BUSINESS-RULES section 6 before touching season ids.
- Dry-run first, report the counts, and wait for my approval.
- Use /data-change for any bulk or destructive data operation (it checks that continuous
  backup is on and records the restore point first).
- Never write to live data, or send non-GET requests to the live site, without my explicit approval.
- Assume a PR preview uses production data and services (not yet confirmed), so do not use it
  to test anything that writes.

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
- Calling methods that do not exist; check they exist first.

## Never
- Force-push, git reset --hard, or skip commit checks (--no-verify).
- Push code to main.
- Delete Azure resources.
- Read .env files.

## Blocks, hooks and permissions
A PreToolUse hook refuses destructive Azure, credential, key and secret access, live-site writes, history-destroying git, edits to hooks and settings, and attempts to turn the hooks off. It asks first for non-GET `az rest` and `gh api`, `func start`, deploys, a push that names main or master, and any `gh pr merge`. Details and rule ids are in `.claude/hooks/guard.ps1`.

- When a command is refused, report the rule to me. A refusal from a hook, a deny rule or the permission classifier is final for that action: tell me which one it was and what you were trying to do. If the task can be done another way that does not need the blocked action, do that. Never retry the blocked action in another spelling or form, and never try to disable, edit or work around a hook.
- Hooks and settings files are changed only by me, by hand. To propose a rule change, put a draft in `.claude/hooks-draft/` and say which files to copy; I copy them in and run `Register-Hook.ps1`.
- The hook is a guardrail against careless commands, not a sandbox. It does not parse commands, so disguised forms get through.
- I choose the permission mode; you cannot change it. Never suggest Bypass permissions mode, and never ask me to switch modes to get past a block.
- If I deny a prompt, stop and report. Do not look for another way to get the same effect.
- Edits to instruction files, hooks and settings can be blocked in Auto mode. If one is, tell me which file and what change you wanted; I will switch modes or make it myself.
