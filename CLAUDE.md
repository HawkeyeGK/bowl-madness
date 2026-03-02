# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

See [ARCHITECTURAL_CONTEXT.md](ARCHITECTURAL_CONTEXT.md) for full architectural deep-dive.

## Infrastructure & Deployment

Hosted on Azure Static Web Apps (Central US). Deployments trigger automatically via GitHub Actions on push to `main`.

**Deployment is explicitly controlled — never push to `main` unless the user asks.** Work is committed locally or pushed to a feature branch. Only merge/push to `main` when the user says they are ready to deploy.

## Git Workflow

### Branches
- **Direct to `main` is the default.** Commit directly on `main` for small, self-contained changes.
- Use a feature branch for changes that are large, multi-step, or otherwise significant (e.g., new features, refactors touching multiple files, anything that would be risky to partially deploy). Suggest a feature branch proactively when a task meets this bar.
- Branch naming: `feature/<short-description>` (e.g., `feature/add-payment-export`).

### Commit Messages — GitHub Issue Linking
Every commit must reference the GitHub issue it belongs to. Use the issue number in the commit message body:
- To **reference** an issue (without closing it): include `#123` in the message.
- To **close** an issue on merge to `main`: use a closing keyword — `Closes #123`, `Fixes #123`, or `Resolves #123` — in the commit message body.

Example commit message:
```
Add CSV export for payment manager

Adds a download button to the Payment Manager page that exports
the current filtered view as a CSV file.

Closes #42

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
```

### Pushing and Deploying
- **Never push to `main` (or merge a feature branch into `main`) without explicit instruction.** Commits accumulate locally or on a feature branch until the user says they are ready to deploy.
- When the user confirms they are ready to deploy: push `main` directly, or merge the feature branch first (`git checkout main && git merge feature/<name>`) then push.
- Pushing a feature branch does not trigger a deployment.

## Development Workflow

### Guiding Principle: Accuracy Over Speed

**Both the main workflow and all sub-agents are expected to pause and ask for clarification whenever intent, scope, or approach is ambiguous.** Making a wrong assumption — especially in a multi-step plan — can be far more costly to unwind than the time it takes to ask a targeted question. No agent or workflow step should proceed on a guess when a question would resolve it. This applies equally to implementation decisions, test expectations, review findings with multiple valid fixes, and deployment scope.

Two tracks, chosen based on the scope of the change.

### Track A — Tactical (small, self-contained changes)

Use for bug fixes, minor UI changes, config updates, or anything confined to one or two files with no architectural impact.

```
Implement → Build Validator → Commit
```

Code Review is optional on this track. Invoke it when the change touches any of: Core domain models, CosmosDB queries, `SecurityHelper` / auth logic, `DateTime` handling, partition key usage, or JSON serialization attributes.

### Track B — Phased Implementation (large features, multi-step plans)

Use for new features, significant refactors, or anything multi-step such as the March Madness extension. Each phase follows this cycle:

```
[In-conversation] Phase planning & scoping
        ↓
Implement phase deliverables
        ↓
Build Validator        ← must pass before continuing
        ↓
QA Agent               ← writes/updates tests, runs full suite
        ↓
Code Review Agent      ← architecture compliance and quality check
        ↓
[Checkpoint] Review with user → approve or revise
        ↓
Commit (references GitHub issue in message body)
        ↓
[Repeat for next phase]
        ↓
[All phases done] Deploy on user's explicit instruction
        ↓
Deployment Validator   ← post-deploy smoke test across roles
```

**Checkpoint criteria** — a phase must satisfy all of these before advancing:
- `dotnet build` succeeds with no errors
- All tests pass
- New business logic has test coverage (QA Agent approved)
- No blocking findings from Code Review Agent
- User has reviewed and approved

### Sub-Agents

Agent prompts live in [.claude/agents/](.claude/agents/). Invoke them via the Agent tool, passing the agent's `.md` file as its system prompt.

| Agent | File | When to invoke |
|---|---|---|
| Build Validator | `.claude/agents/build-validator.md` | After every implementation step on both tracks |
| QA Agent | `.claude/agents/qa-agent.md` | At each Track B phase checkpoint |
| Code Review Agent | `.claude/agents/code-review.md` | At each Track B phase checkpoint; optionally on Track A for high-risk changes |
| Deployment Validator | `.claude/agents/deployment-validator.md` | After every production deployment |

## Available CLI Tools

- **Azure CLI** (`az`) — authenticated for the project subscription
- **Git** (`git`) — use for all source control operations

## Commands

```bash
# Build the entire solution
dotnet build BowlPoolManager.sln

# Run all tests
dotnet test BowlPoolManager.Tests/BowlPoolManager.Tests.csproj

# Run a single test class
dotnet test BowlPoolManager.Tests/BowlPoolManager.Tests.csproj --filter "FullyQualifiedName~ScoringEngineTests"

# Run a single test method
dotnet test BowlPoolManager.Tests/BowlPoolManager.Tests.csproj --filter "FullyQualifiedName~ScoringEngineTests.Calculate_ShouldReturnCorrectScore_ForFinalGames"

# Run the API locally (requires Azure Functions Core Tools)
cd BowlPoolManager.Api && func start

# Run the Client locally
cd BowlPoolManager.Client && dotnet run
```

## Project Structure

Four projects in one solution (`BowlPoolManager.sln`):

| Project | Target | Type | Purpose |
|---|---|---|---|
| `BowlPoolManager.Core` | net8.0 | Class library | Shared domain models, DTOs, scoring engines, constants |
| `BowlPoolManager.Api` | net8.0 | Azure Functions v4 isolated | REST API / serverless back-end |
| `BowlPoolManager.Client` | net10.0 | Blazor WebAssembly | Front-end SPA |
| `BowlPoolManager.Tests` | net10.0 | xUnit test project | Tests for Core and Api logic |

## Testing

- Framework: xUnit with FluentAssertions and Moq.
- Test files mirror the source: `BowlPoolManager.Tests/Core/` tests Core helpers; `BowlPoolManager.Tests/Api/` tests API helpers.
- The test project references both `BowlPoolManager.Api` and `BowlPoolManager.Core`.
- Method naming: `MethodName_ShouldExpectedBehavior_WhenCondition` (e.g. `Calculate_ShouldReturnCorrectScore_ForFinalGames`).
- Always use FluentAssertions (`result.Should().Be(...)`). Use Moq for all dependencies.

## Terminal Environment
Running on Windows with PowerShell. Use PowerShell-compatible 
commands, not Bash syntax.
