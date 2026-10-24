# Architectural Context - Bowl Madness and Hoops Madness

This document captures the architectural decisions and patterns of the project so that human developers and AI agents stay in sync across the Blazor Client, Azure Functions and Cosmos DB. Intended behavior is in docs/BUSINESS-RULES.md; infrastructure is in docs/INFRASTRUCTURE.md.

## 1. System Overview

The solution is one codebase that serves two sites: Bowl Madness (college football bowl pools, bowl-madness.com) and Hoops Madness (NCAA basketball bracket pools, hoops-madness.com). The site shown is chosen by domain (Section 11). The solution has four projects:
- **`BowlPoolManager.Client`**: A Blazor WebAssembly (WASM) front-end providing the user interface for both sites.
- **`BowlPoolManager.Api`**: An Azure Functions back-end, acting as the serverless API layer.
- **`BowlPoolManager.Core`**: A shared .NET class library containing domain models, DTOs, validation, scoring engines and constants used by both the client and the API.
- **`BowlPoolManager.Tests`**: xUnit tests for Core and Api.

The Blazor Client interacts over HTTP with the Azure Functions, which in turn communicate with an Azure Cosmos DB instance to persist data.

## 2. Data Schema (CosmosDB)

The application utilizes CosmosDB with an approach abstracted by `CosmosRepositoryBase`. The `CosmosClient` is registered as a singleton in `BowlPoolManager.Api/Program.cs` with `CosmosSerializationOptions { PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase }`. No custom serializer is configured, so the Cosmos SDK uses its default Newtonsoft.Json-based serializer; `[JsonProperty]` names take precedence over the naming policy.

### Container/Partition Key Strategy
- **Partition Keys**: The codebase dynamically passes the Partition Key (as a string) to the repository methods (e.g., `UpsertDocumentAsync<T>(item, partitionKey)`). 
- **Entity Specifics**: 
  - **Games / Entries**: Partitioned largely by `seasonId` (e.g., in `GameRepository.cs` transactions use `new PartitionKey(seasonId)`).
  - **Configurations**: Uses explicit string literals like `"Config_Teams_FBS"` for both the document ID and Partition Key.

### Key Domain Models / DTOs
Models live in `BowlPoolManager.Core/Domain` and map directly to Cosmos documents. Every property must carry both `[JsonProperty]` (Newtonsoft.Json) and `[JsonPropertyName]` (System.Text.Json) attributes — see Section 5.
- `BowlGame`, `BowlPool`: a football game and a football pool.
- `BracketEntry`: a user's picks in a pool. Used for both sports; the `type` field tells them apart: `"BracketEntry"` for football and `"HoopsBracketEntry"` for hoops (`Constants.DocumentTypes`). There is no separate `HoopsBracketEntry` class. Hoops entries are written by `HoopsEntryFunctions.CreateHoopsEntry`, which forces the type and takes the season from the pool, and are read and written through `HoopsEntryRepository`, which filters on that type. Both kinds live in the `Picks` container, partitioned by `seasonId`. A hoops entry's `seasonId` is its pool's, which for some pools is the text "2026" (docs/BUSINESS-RULES.md section 6).
- `UserProfile`: details about the logged-in user, including role and disabled flag.
- Hoops models: see Section 12.

*Example Domain Model JSON Mapping Context:*
```csharp
// Cosmos serializer options defined in Program.cs (default Newtonsoft-based serializer):
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
- All function triggers use `AuthorizationLevel.Anonymous`. Identity comes from Azure Static Web Apps, which adds an `x-ms-client-principal` header to API calls; `SecurityHelper.ParseSwaHeader` decodes it. The user's role is then loaded from the `Players` container (`IUserRepository.GetUserAsync`). The role is never taken from the header.
- The caller tiers are Public, Owner, Admin and SuperAdmin. What each may do is defined in docs/BUSINESS-RULES.md section 1; this document describes only how the code checks them:

| Tier | Check in the function |
|---|---|
| Public | No check. Read-only data that anyone may see. |
| Owner | `ParseSwaHeader`; the function loads the entry and compares its `UserId` with the caller's. If they differ, falls through to the Admin check. |
| Admin | `ParseSwaHeader`, `userRepo.GetUserAsync`, then `SecurityHelper.IsAdmin(user)` (true for Admin and SuperAdmin). |
| SuperAdmin | `SecurityHelper.ValidateSuperAdminAsync(req, _userRepo)`; returns 401 for no login and 403 for the wrong role. |

- `staticwebapp.config.json` protects only the `/admin/*` pages and routes `/login` and `/logout`. It protects nothing under `/api/*`. Every rule is enforced by the function itself.
- The Client uses `StaticWebAppsAuthenticationStateProvider` alongside standard Blazor `AuthorizationCore` to manage identity. This is for display only.
- Known deviations from the rules (functions with no check, disabled accounts not enforced) are tracked in GitHub Issues, not here.

## 4. State Management (Blazor Client)

The Blazor client avoids heavy external state libraries (like Fluxor) in favor of simple, scoped services.
- **`AppState` Service**: A scoped service class `AppState.cs` is registered in `Program.cs`. 
  - It uses standard C# events (`public event Action? OnChange;`) and notification methods (`public void NotifyDataChanged() => OnChange?.Invoke();`).
  - Components subscribe to `AppState.OnChange` to trigger `StateHasChanged()`.
- **Domain Services**: Dedicated interaction services (e.g., `PoolService`, `SeasonService`) are injected as `Scoped` dependencies to handle localized network calling and mapping logic.

## 5. Development Constraints ("Rules of the Road")

To maintain architectural purity, follow these rules:
- **Serialization**: Domain models in `BowlPoolManager.Core/Domain` must carry **both** `[JsonProperty("camelCaseName")]` (Newtonsoft.Json) **and** `[JsonPropertyName("camelCaseName")]` (System.Text.Json) on every property. Neither attribute family may be removed or added alone. New properties must always include both. The Cosmos SDK serializes documents with its default Newtonsoft.Json-based serializer (CamelCase policy), so `[JsonProperty]` controls the stored names. System.Text.Json is used by the Blazor client and by some function code that parses request bodies and the login header. Both attributes are required for cross-stack compatibility.
- **Shared Models**: All models shared between the API and Client must live in `BowlPoolManager.Core`. Do not duplicate definitions.
- **Cosmos Optimization**: All Cosmos queries should try to include the Partition Key. Where batch operations occur, ensure the batch targets a specific `PartitionKey`.
- **Date Handling**: See CLAUDE.md (Dates and models) and docs/BUSINESS-RULES.md section 7. Not restated here.
- **Security Check**: Every function that writes data or returns restricted data checks the caller on the server before any other logic, using the pattern for its tier in Section 3. SuperAdmin-only functions call `SecurityHelper.ValidateSuperAdminAsync(req, _userRepo)` as their first statement. Never remove or move a check during a refactor.
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
- `BowlPoolManager.Tests` references `BowlPoolManager.Api`, `BowlPoolManager.Core` and `BowlPoolManager.Client`.
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

## 11. Site Selection by Domain

One Blazor build serves both sites. `ISiteContext` / `SiteContext` (Client/Services) exposes `ActiveSport` (`Sport.Football` or `Sport.Basketball`). It is decided from the browser host name: a host containing `hoops-madness` is Basketball, one containing `bowl-madness` is Football. On localhost it falls back to a `/basketball` path prefix, and in the Development environment a value stored in browser local storage (`devSportOverride`) overrides both. `NavMenu` shows sport-specific links and pages (for example `/hoops/leaderboard` versus `/leaderboard`) from `ActiveSport`. Both domains point at the same Static Web App (docs/INFRASTRUCTURE.md).

## 12. Hoops (Basketball) Models and Services

- **Core models**: `HoopsPool`, `HoopsGame` (implements `IScorable`), `TournamentRound`, `Sport`; `BracketEntry` doubles as the hoops entry (Section 2). Bracket creation input is `BracketGenerationRequest` / `FirstFourEntry` in `Core/Dtos`.
- **Validation and scoring (Core)**: `HoopsEntryUpdateValidator`, `HoopsWhatIfScoringEngine` (Path to Victory simulation). Real leaderboard ranking uses the shared `ScoringEngine` (Section 7).
- **Api repositories**: `HoopsPoolRepository`, `HoopsGameRepository`, `HoopsEntryRepository`. Hoops pools and games are stored in the `Seasons` container with document types `HoopsPool` and `HoopsGame`.
- **Api services**: `BracketGeneratorService` builds the bracket game tree from a `BracketGenerationRequest`; `HoopsGameScoringService` refreshes live scores (throttled) and propagates winners to later rounds.
- **Api functions**: `HoopsPoolFunctions`, `HoopsGameFunctions`, `HoopsEntryFunctions`, `HoopsLeaderboardFunctions`, `HoopsArchiveFunctions`.
- **Client services**: `HoopsPoolService`, `HoopsGameService`. Bracket UI components are in `Client/Components` (`BracketPickerComponent`, `RegionBracket`, `FinalFourBracket`, `FirstFourBracket`, `BracketMatchup`).

## 13. External Data Services

Names and purpose only; credentials are app settings (docs/INFRASTRUCTURE.md).
- **`CfbdService`**: College Football Data API (api.collegefootballdata.com). Football schedules, scores and team lists.
- **`BasketballDataService`**: College Basketball Data API (api.collegebasketballdata.com). Basketball scoreboard and teams for live scores and game linking.
- **`EspnDataService`**: ESPN public endpoints. Basketball team list and team search, used for logos and name mapping.

All three are called through typed `HttpClient` registrations in `Program.cs`.

## 14. Application Insights and Health Check

- **API**: `Program.cs` calls `AddApplicationInsightsTelemetryWorkerService()` and `ConfigureFunctionsApplicationInsights()`; `host.json` enables sampling (Request excluded). The connection string is an app setting.
- **Client**: the Application Insights JavaScript SDK loader is in `wwwroot/index.html`. No code calls `trackPageView`, so Blazor route changes are not recorded as separate page views (known gap).
- **Health check**: `GET /api/healthcheck` (`HealthCheckFunction`) returns a `HealthCheckResponseDto` (status, source, message, UTC timestamp). It touches nothing in Cosmos, so it shows only that the API host is up.

## 15. Legacy Container and Migration Endpoints

Before the current containers (Players, Seasons, Picks, PoolArchives, Configuration), all data was in one container named `MainContainer` (partition key `/id`). It still exists in the database (confirmed with a read-only container listing on 2026-10-24). `MigrationRepository` reads it and `MigrationFunctions` exposes: `GET /api/migration/ping` (open, returns "Pong"), `POST /api/migration/analyze` and `POST /api/migration/execute` (SuperAdmin; analyze legacy games/pools/entries; execute converts legacy entries into football `BracketEntry` documents using season, pool, game and team mappings sent in the request, saved through `EntryRepository`). `InitializeDatabase` (`POST /api/Admin/Initialize`) creates the five current containers only; it does not create `MainContainer`. Do not add startup initialization (Section 5).
