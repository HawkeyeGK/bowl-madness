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
- Ask me about intended behavior, never about code. Decide technical questions yourself
  and explain the choice in your summary.

## Git
- Code changes go on a branch with a pull request. Never merge. Never push code to main.
- Changes that only add or edit documents may be committed straight to main. Ask before pushing.
- Don't commit until I say the change is what I want, or I ask for a checkpoint.
  Group related changes into one commit unless I say otherwise.
- Reference the issue in the commit message when one exists (Closes #N or Refs #N).
- Stage files by name. Do not use git add . or git add -A.
- Before opening a PR or pushing:
  1. Build and test. No new failures beyond the failures already tracked in open issues.
  2. Run the code-review agent. Fix critical and high findings. Stop after about three
     rounds and report what is unresolved in plain language.
  3. Give me a summary: what changed, why, what could go wrong, what was tested.
- Ask before git push and again before gh pr create.

## Bugs
- Write a failing test first. Run the tests on unchanged code to get a baseline.
- Use a git worktree for the baseline, not git stash.

## Business rules
- docs/BUSINESS-RULES.md is the intended behavior. Where code disagrees, the code is the bug.
- Read the relevant section before changing pool, entry, scoring or permission code.
- Before changing code in an area it covers, check open GitHub issues for that area.
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
- Never write to live data, or send non-GET requests to the live site, without my explicit approval.
  I take a fresh export before data work.

## Security
- Every function that writes data checks the caller first, on the server, before any other
  logic. Hiding a button is not a protection.
- No secrets in code, logs or docs. The Cosmos connection string stays in app settings.

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
