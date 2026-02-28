# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

See [ARCHITECTURAL_CONTEXT.md](ARCHITECTURAL_CONTEXT.md) for full architectural deep-dive.

## Infrastructure & Deployment

Hosted on Azure Static Web Apps (Central US). Deployments trigger automatically via GitHub Actions on merge to `main`. Do not suggest manual deployment steps.

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
- All projects target `net10.0`.
