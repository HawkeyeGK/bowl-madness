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

| Project | Type | Purpose |
|---|---|---|
| `BowlPoolManager.Core` | Class library | Shared domain models, DTOs, scoring engines, constants |
| `BowlPoolManager.Api` | Azure Functions v4 (.NET 8 isolated) | REST API / serverless back-end |
| `BowlPoolManager.Client` | Blazor WebAssembly (.NET 8) | Front-end SPA |
| `BowlPoolManager.Tests` | xUnit test project | Tests for Core and Api logic |

## Architecture Summary

### Data Layer (CosmosDB)
All repositories extend `CosmosRepositoryBase` ([BowlPoolManager.Api/Infrastructure/CosmosRepositoryBase.cs](BowlPoolManager.Api/Infrastructure/CosmosRepositoryBase.cs)), which wraps upsert, get, query, and delete against a named CosmosDB container. The `CosmosClient` is configured as a singleton in [BowlPoolManager.Api/Program.cs](BowlPoolManager.Api/Program.cs) with `CamelCase` serialization policy.

**Containers** (defined in `Constants.Database`):
- `Players` — user profiles
- `Seasons` — games and pools, partitioned by `seasonId`
- `Picks` — bracket entries
- `PoolArchives` — concluded pool snapshots
- `Configuration` — team configs (e.g., partition key `"Config_Teams_FBS"`)

### API Layer (Azure Functions)
HTTP-triggered functions in `BowlPoolManager.Api/Functions/`. All triggers use `AuthorizationLevel.Anonymous`; security is enforced manually via `SecurityHelper.ValidateSuperAdminAsync()` at the start of restricted endpoints. The `x-ms-client-principal` base64 header (injected by Azure Static Web Apps) is parsed by `SecurityHelper.ParseSwaHeader()`.

### Client Layer (Blazor WASM)
- **Authentication**: `StaticWebAppsAuthenticationStateProvider` (custom, in `Security/`) reads the `/.auth/me` endpoint.
- **State management**: `AppState` (scoped service) exposes `event Action? OnChange` — components subscribe to it and call `StateHasChanged()` on change.
- **Domain services**: `PoolService`, `SeasonService`, `ConfigurationService` are scoped and handle all HTTP calls to the API.

### Scoring Logic (Core)
- `ScoringEngine` ([BowlPoolManager.Core/Helpers/ScoringEngine.cs](BowlPoolManager.Core/Helpers/ScoringEngine.cs)): Calculates real leaderboard rankings from actual game results. Supports configurable tiebreakers (`CorrectPickCount` or `ScoreDelta`) via `BowlPool.PrimaryTieBreaker`/`SecondaryTieBreaker`.
- `WhatIfScoringEngine` ([BowlPoolManager.Core/Helpers/WhatIfScoringEngine.cs](BowlPoolManager.Core/Helpers/WhatIfScoringEngine.cs)): Simulates outcomes by merging a `simulatedWinners` dictionary over real results — used by the "Path to Victory" (`/what-if`) page.

## Key Patterns & Constraints

- **Serialization**: Domain models carry both `[JsonProperty]` (Newtonsoft) and `[JsonPropertyName]` (System.Text.Json) attributes to remain compatible across the stack. The CosmosDB SDK uses System.Text.Json with CamelCase. Do not remove either attribute family from domain models.
- **Shared models only in Core**: Never duplicate model definitions between projects.
- **Cosmos queries must include partition key** where possible. Batch operations must target a single `PartitionKey`.
- **DateTime**: Always use `DateTime.UtcNow`; treat all `DateTime` values as UTC.
- **Security**: Call `SecurityHelper.ValidateSuperAdminAsync()` at the top of any restricted function before executing business logic.
- **Database initialization**: Triggered manually via the Admin Dashboard (`InfrastructureFunctions.cs`), not at startup.

## Testing

- Framework: xUnit with FluentAssertions and Moq.
- Test files mirror the source: `BowlPoolManager.Tests/Core/` tests Core helpers; `BowlPoolManager.Tests/Api/` tests API helpers.
- The test project references both `BowlPoolManager.Api` and `BowlPoolManager.Core`.
- **Target framework split**: `Api` and `Core` target `net8.0`; `Client` and `Tests` target `net10.0`. A net10.0 project can reference net8.0 assemblies, so this compiles and runs cleanly. The Api is capped at net9.0 max — not because of Azure Functions (which supports net10), but because the **Azure Static Web Apps Oryx build system** (used to build and deploy the API) does not yet support net10.0.
