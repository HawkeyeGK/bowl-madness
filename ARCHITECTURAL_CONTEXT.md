# Architectural Context - BowlPoolManager

This document serves as the "Long-Term Memory" for the BowlPoolManager project, capturing core architectural decisions and patterns to ensure that both human developers and AI agents maintain synchronization across the Blazor Client, Azure Functions, and CosmosDB.

## 1. System Overview

BowlPoolManager is a serverless application designed to manage bowl game pools. The system is split across three primary projects:
- **`BowlPoolManager.Client`**: A Blazor WebAssembly (WASM) front-end providing the user interface.
- **`BowlPoolManager.Api`**: An Azure Functions back-end, acting as the serverless API layer.
- **`BowlPoolManager.Core`**: A shared .NET class library containing domain models, DTOs, and constants used by both the client and the API.

The Blazor Client interacts over HTTP with the Azure Functions, which in turn communicate with an Azure CosmosDB instance to persist data.

## 2. Data Schema (CosmosDB)

The application utilizes CosmosDB with an approach abstracted by `CosmosRepositoryBase`. The `CosmosClient` is configured globally in `BowlPoolManager.Api/Program.cs` using `System.Text.Json` with a CamelCase naming policy.

### Container/Partition Key Strategy
- **Partition Keys**: The codebase dynamically passes the Partition Key (as a string) to the repository methods (e.g., `UpsertDocumentAsync<T>(item, partitionKey)`). 
- **Entity Specifics**: 
  - **Games / Entries**: Partitioned largely by `seasonId` (e.g., in `GameRepository.cs` transactions use `new PartitionKey(seasonId)`).
  - **Configurations**: Uses explicit string literals like `"Config_Teams_FBS"` for both the document ID and Partition Key.

### Key Domain Models / DTOs
Models live in `BowlPoolManager.Core/Domain` and map directly to Cosmos documents. Every property must carry both `[JsonProperty]` (Newtonsoft.Json) and `[JsonPropertyName]` (System.Text.Json) attributes — see Section 5.
- `BowlGame`: Represents a football game.
- `BowlPool`: Represents a user-created pool.
- `BracketEntry`: A user's picks within a specific pool.
- `UserProfile`: Details about the logged-in user.

*Example Domain Model JSON Mapping Context:*
```csharp
// System.Text.Json policy defined in builder:
var clientOptions = new CosmosClientOptions 
{ 
    SerializerOptions = new CosmosSerializationOptions { PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase } 
};
```

## 3. API Contract

The `BowlPoolManager.Api` acts as the intermediary between the Client and Data layers.

### Function Triggers
The system heavily limits function triggers to standard HTTP.
- **HTTP Triggers**: Standard REST operations (e.g., `[HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", "put", "delete")]`).

### Endpoint Naming Convention
- Routes follow standard REST principles, mostly falling back to default function names mapped to `/api/{FunctionName}` unless customized via the `Route` attribute. 
- Example customized routes:
  - `[HttpTrigger(..., Route = "DeletePool/{poolId}")]` maps to `/api/DeletePool/{poolId}`
  - `[HttpTrigger(..., Route = "Pools/{poolId}/ToggleConclusion")]` maps to `/api/Pools/{poolId}/ToggleConclusion`

### Authentication/Authorization Patterns
- The Azure Functions triggers are set to `AuthorizationLevel.Anonymous`.
- **Manual Assertion**: Security checks are handled manually inside the endpoints using `SecurityHelper.ValidateSuperAdminAsync(req, _userRepo);`. 
- The Client uses `StaticWebAppsAuthenticationStateProvider` alongside standard Blazor `AuthorizationCore` to manage identity.

## 4. State Management (Blazor Client)

The Blazor client avoids heavy external state libraries (like Fluxor) in favor of simple, scoped services.
- **`AppState` Service**: A scoped service class `AppState.cs` is registered in `Program.cs`. 
  - It uses standard C# events (`public event Action? OnChange;`) and notification methods (`public void NotifyDataChanged() => OnChange?.Invoke();`).
  - Components subscribe to `AppState.OnChange` to trigger `StateHasChanged()`.
- **Domain Services**: Dedicated interaction services (e.g., `PoolService`, `SeasonService`) are injected as `Scoped` dependencies to handle localized network calling and mapping logic.

## 5. Development Constraints ("Rules of the Road")

To maintain architectural purity, follow these rules:
- **Serialization**: Domain models in `BowlPoolManager.Core/Domain` must carry **both** `[JsonProperty("camelCaseName")]` (Newtonsoft.Json) **and** `[JsonPropertyName("camelCaseName")]` (System.Text.Json) on every property. Neither attribute family may be removed or added alone. New properties must always include both. The CosmosDB SDK uses System.Text.Json with CamelCase; other deserialization paths in the stack rely on Newtonsoft.Json. Both are required for cross-stack compatibility.
- **Shared Models**: All models shared between the API and Client must live in `BowlPoolManager.Core`. Do not duplicate definitions.
- **Cosmos Optimization**: All Cosmos queries should try to include the Partition Key. Where batch operations occur, ensure the batch targets a specific `PartitionKey`.
- **Date Handling**: Treat `DateTime` properties as UTC (`DateTime.UtcNow`).
- **Security Check**: For restricted endpoints, call `SecurityHelper.ValidateSuperAdminAsync(req, _userRepo)` as the very first statement before any business logic executes.
- **Database Initialization**: Container creation and schema setup are triggered manually via the Admin Dashboard (`InfrastructureFunctions.cs`). Do not add startup initialization logic.

## 6. Print Bracket Layout Pattern

The printable bracket page ([HoopsPrintBracket.razor](BowlPoolManager.Client/Pages/HoopsPrintBracket.razor)) uses a specific layout strategy for arranging regions and rendering bracket columns. Any future print or export view must follow this pattern.

### Region Side Assignment

Regions are split into left/right halves by mirroring the logic in `FinalFourBracket.GetStableSortKey`:

```csharp
private string GetFfSortKey(HoopsGame ffGame) =>
    games.Where(g => g.Round == TournamentRound.Elite8 && g.NextGameId == ffGame.Id)
         .Select(g => g.Region ?? "")
         .Where(r => r.Length > 0)
         .OrderBy(r => r)
         .FirstOrDefault() ?? ffGame.Id;
```

The Final Four game with the **alphabetically-earlier sort key** gets its two feeder regions assigned to the **left side**; the other FF game's regions go to the **right side**. This guarantees the print bracket always pairs regions that face each other in a semifinal on the same side — matching the interactive `FinalFourBracket` layout exactly.

### Column Direction

- **Left-side regions**: columns render R64 → R32 → S16 → E8 (left to right). The E8 column carries `no-right-line` to suppress the outbound connector stub.
- **Right-side regions**: columns render E8 → S16 → R32 → R64 (left to right, mirrored). The region container receives the `.reversed` CSS class. The E8 column carries `no-left-line`.

### Connector Stub CSS

Left-side regions use the standard `::after` right-pointing stubs. Right-side regions suppress `::after` and use `::before` left-pointing stubs:

```css
/* Right-side mirrored stubs */
.reversed .match-card::after { display: none; }
.reversed .match-card:not(.no-left-line)::before {
    content: '';
    position: absolute;
    left: -4px;
    top: 50%;
    width: 4px;
    height: 1px;
    background: #999;
}
```

### Pick Highlighting

When an `EntryId` is provided, each match card is annotated with a CSS class based on the user's pick relative to the actual game result:
- `user-pick-correct` (green) — user's pick matches the game winner
- `user-pick-incorrect` (red) — game has a winner and user's pick lost
- `user-pick-pending` (blue) — user made a pick but the game has no result yet

## 7. Scoring Logic (Core)

Two pure calculation engines live in `BowlPoolManager.Core/Helpers/`:

- **`ScoringEngine`** ([BowlPoolManager.Core/Helpers/ScoringEngine.cs](BowlPoolManager.Core/Helpers/ScoringEngine.cs)): Calculates real leaderboard rankings from actual game results. Supports configurable tiebreakers (`CorrectPickCount` or `ScoreDelta`) via `BowlPool.PrimaryTieBreaker`/`SecondaryTieBreaker`.
- **`WhatIfScoringEngine`** ([BowlPoolManager.Core/Helpers/WhatIfScoringEngine.cs](BowlPoolManager.Core/Helpers/WhatIfScoringEngine.cs)): Simulates outcomes by merging a `simulatedWinners` dictionary over real results — used by the "Path to Victory" (`/what-if`) page.

## 8. Cross-Project Dependencies

- `BowlPoolManager.Client` depends directly on `BowlPoolManager.Core`.
- `BowlPoolManager.Api` depends directly on `BowlPoolManager.Core`.
- `BowlPoolManager.Tests` references both `BowlPoolManager.Api` and `BowlPoolManager.Core`.
- The Client never directly references the API or data packages; communication is strictly over HTTP. The API hides CosmosDB completely from the Core models.

## 9. Target Framework & Build Constraints

| Project | Target Framework |
|---|---|
| `BowlPoolManager.Core` | `net8.0` |
| `BowlPoolManager.Api` | `net8.0` |
| `BowlPoolManager.Client` | `net10.0` |
| `BowlPoolManager.Tests` | `net10.0` |

A net10.0 project can reference net8.0 assemblies, so this compiles and runs cleanly.

**API ceiling**: The API is capped at `net9.0` maximum — not because Azure Functions lacks support for net10, but because the **Azure Static Web Apps Oryx build system** (which builds and deploys the API) does not yet support net10.0. Do not upgrade the Api or Core target frameworks beyond net9.0 without verifying Oryx support first.

## 10. Cross-Sport Archive Conventions

### Shared Model Extension Pattern
`PoolArchive` and `ArchiveGame` in `BowlPoolManager.Core/Domain/PoolArchive.cs` serve **both** football and basketball. Sport-specific fields are added as **nullable** properties so existing documents are unaffected:

```csharp
// Basketball-specific — null for football archives
public TournamentRound? Round { get; set; }
public string? Region { get; set; }
public int? TeamHomeSeed { get; set; }
public int? TeamAwaySeed { get; set; }
```

The rule: **extend shared models with nullable sport-specific fields rather than creating parallel model hierarchies**. This keeps the `PoolArchives` Cosmos container and `IArchiveRepository` unified. New fields must carry both `[JsonProperty]` and `[JsonPropertyName]` per Section 5.

### Archive ID Namespace Convention
Both sports write to the same `PoolArchives` container (partition key: `seasonId`). IDs use a sport-specific prefix to prevent collision on cross-partition reads:

| Sport | ID Format | Example |
|---|---|---|
| Football | `"Archive_{poolId}"` | `Archive_abc123` |
| Basketball | `"HoopsArchive_{poolId}"` | `HoopsArchive_abc123` |

Any future sport must introduce its own prefix (e.g., `"BaseballArchive_{poolId}"`). The `GetArchiveAsync(id)` repository method does a cross-partition query by ID — acceptable at archive volume (one read per page load, low total document count).
