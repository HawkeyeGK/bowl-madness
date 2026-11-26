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

### Containers (database `BowlMadnessDb`)
| Container | Partition key | What it holds |
|---|---|---|
| Players | /id | User profiles |
| Seasons | /seasonId | Seasons, bowl and hoops pools, bowl and hoops games |
| Picks | /seasonId | Bowl entries, hoops entries (HoopsBracketEntry) |
| PoolArchives | /seasonId | Archives (ids Archive_{poolId}, HoopsArchive_{poolId}) |
| Configuration | /id | Team lists, API integration settings |

### Container/Partition Key Strategy
- **Partition Keys**: The codebase dynamically passes the Partition Key (as a string) to the repository methods (e.g., `UpsertDocumentAsync<T>(item, partitionKey)`). 
- **Entity Specifics**: 
  - **Games / Entries**: Partitioned largely by `seasonId` (e.g., in `GameRepository.cs` transactions use `new PartitionKey(seasonId)`).
  - **Configurations**: Uses explicit string literals like `"Config_Teams_FBS"` for both the document ID and Partition Key.

### Key Domain Models / DTOs
Models live in `BowlPoolManager.Core/Domain` and map directly to Cosmos documents. Every property must carry both `[JsonProperty]` (Newtonsoft.Json) and `[JsonPropertyName]` (System.Text.Json) attributes — see Section 5.
- `BowlGame`, `BowlPool`: a football game and a football pool.
- `BracketEntry`: a user's picks in a pool. Used for both sports; the `type` field tells them apart: `"BracketEntry"` for football and `"HoopsBracketEntry"` for hoops (`Constants.DocumentTypes`). There is no separate `HoopsBracketEntry` class. Hoops entries are written by `HoopsEntryFunctions.CreateHoopsEntry`, which forces the type and takes the season from the pool, and are read and written through `HoopsEntryRepository`, which filters on that type. Both kinds live in the `Picks` container, partitioned by `seasonId`. A hoops entry's `seasonId` is its pool's, which for some pools is the text "2026" (docs/BUSINESS-RULES.md section 6). `ownerName` (nullable, Release 2, football only) holds the owner's name when an Admin keyed the entry for someone with no account; `userId` is then empty. Phase 1.1 code ignores it, and `EntryVisibility.ForViewer` does not copy it for non-owners.
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

- `staticwebapp.config.json` protects only the `/admin/*` pages, routes `/logout`, and sends a 401 to `/login`, the sign-in chooser page of the app (Google and Microsoft side by side; nothing in the routing file names a provider). It protects nothing under `/api/*`. Every rule is enforced by the function itself.
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
- **`BracketSimulator`** ([BowlPoolManager.Core/Helpers/BracketSimulator.cs](BowlPoolManager.Core/Helpers/BracketSimulator.cs)): The one football bracket simulator behind "Path to Victory" (`/what-if`). Takes games, entries, the pool and the user's chosen winners; returns each game's teams and winner, each entry's score, Max, rank and whether it can still finish in the top 3, and each pick's state (won, lost, dead, open). Ranking and Max go through `ScoringEngine`, so with no chosen winners it equals the leaderboard.
- **`BracketSlots`** and **`BracketPropagation`** ([BracketSlots.cs](BowlPoolManager.Core/Helpers/BracketSlots.cs), [BracketPropagation.cs](BowlPoolManager.Core/Helpers/BracketPropagation.cs)): The one slot rule (which slot of the next game a feeder's winner fills) and the copy-forward of a winner or the "Winner of ..." reset. Used by `GameScoringService`, `HoopsGameScoringService`, the simulator, `ScoreboardCard`, `PrintBracket` and `Admin/GameResults`. Hoops Path to Victory still uses `HoopsWhatIfScoringEngine`.
- **`EntryPickRules`**, **`InviteCodeLookup`** and **`DateTimeHelper.ToCentralLabel`** ([EntryPickRules.cs](BowlPoolManager.Core/Helpers/EntryPickRules.cs), [InviteCodeLookup.cs](BowlPoolManager.Core/Helpers/InviteCodeLookup.cs), [DateTimeHelper.cs](BowlPoolManager.Core/Helpers/DateTimeHelper.cs)): `EntryPickRules` works out which teams a football entry may pick in each game from its own earlier picks (slots come from `BracketSlots`); `EntrySubmission.Validate` uses it on the server, and `MyPicks` and `Admin/EntryForm` use it to disable slots and clear dependent picks. `InviteCodeLookup` picks the pool a code opens (current season first). `ToCentralLabel` is the one way to show a stored time (Central, with "CT"). The `MoveEntry` function (EntryFunctions, POST, Admin only; query `entryId`, `seasonId`, `newPoolId`) moves a football entry to another open pool of the same season.

## 8. Cross-Project Dependencies

- `BowlPoolManager.Client` depends directly on `BowlPoolManager.Core`.
- `BowlPoolManager.Api` depends directly on `BowlPoolManager.Core`.
- `BowlPoolManager.Tests` references `BowlPoolManager.Api`, `BowlPoolManager.Core` and `BowlPoolManager.Client`.
- The Client never directly references the API or data packages; communication is strictly over HTTP. The API hides CosmosDB completely from the Core models.

## 9. Target Framework & Build Constraints

| Project | Target Framework |
|---|---|
| `BowlPoolManager.Core` | `net10.0` |
| `BowlPoolManager.Api` | `net10.0` |
| `BowlPoolManager.Client` | `net10.0` |
| `BowlPoolManager.Tests` | `net10.0` |

The Api project is Azure Functions v4 on the isolated worker (`AzureFunctionsVersion` V4, `OutputType` Exe, `Microsoft.Azure.Functions.Worker` packages, build SDK `Microsoft.Azure.Functions.Worker.Sdk` 2.0.7). All four projects target net10.0. `BowlPoolManager.Client/wwwroot/staticwebapp.config.json` sets `platform.apiRuntime` to `dotnet-isolated:10.0`; keep it matched to the Api target framework. Microsoft's current guide names `Azure.Functions.Sdk` as the .NET 10 build SDK; moving to it is a deferred follow-up.

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

One Blazor build serves both sites. `ISiteContext` / `SiteContext` (Client/Services) exposes `ActiveSport` (`Sport.Football` or `Sport.Basketball`). It is decided from the browser host name: a host containing `hoops-madness` is Basketball, one containing `bowl-madness` is Football. On localhost it falls back to a `/basketball` path prefix, and in the Development environment a value stored in browser local storage (`devSportOverride`) overrides both. There is no switch in the UI since Release 2 (owner decision, 2026-11-23); set the value in the browser, or open the site as `hoops-madness.localhost`. The layout (`AppHeader`, `BottomNav`) shows sport-specific links (for example `/hoops/leaderboard` versus `/leaderboard`) from `ActiveSport`, through `SiteNavigation` in Core (section 18). Both domains point at the same Static Web App (docs/INFRASTRUCTURE.md).

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

- **API**: `Program.cs` calls `AddApplicationInsightsTelemetryWorkerService()` and `ConfigureFunctionsApplicationInsights()`, then `builder.Logging.AllowAppInformationLogs()` (`Helpers/ApplicationInsightsLogging.cs`). The SDK's default filter sends only Warning and above to Application Insights. The helper adds Information for categories under `BowlPoolManager.Api`; every logger in the API is `ILogger<T>` or `ILoggerFactory.CreateLogger<T>()`, so those are the categories in use. Framework categories (`Microsoft.*`, `System.*`) stay at Warning. `host.json` sets sampling for the Functions host (Request excluded) and its log levels apply to the host, not the worker's `ILogger` output. The connection string is an app setting.
- **Client**: the Application Insights JavaScript SDK loader is in `wwwroot/index.html`. `enableAutoRouteTracking: true` is set in the loader, so each Blazor route change sends a page view.
- **Health check**: `GET /api/healthcheck` (`HealthCheckFunction`) returns a `HealthCheckResponseDto` (status, source, message, UTC timestamp). It touches nothing in Cosmos, so it shows only that the API host is up.

## 15. Legacy Container and Migration Endpoints

Before the current containers (Players, Seasons, Picks, PoolArchives, Configuration), all data was in one container named `MainContainer` (partition key `/id`). It still exists in the database (confirmed with a read-only container listing on 2026-10-24). `MigrationRepository` reads it and `MigrationFunctions` exposes: `GET /api/migration/ping` (open, returns "Pong"), `POST /api/migration/analyze` and `POST /api/migration/execute` (SuperAdmin; analyze legacy games/pools/entries; execute converts legacy entries into football `BracketEntry` documents using season, pool, game and team mappings sent in the request, saved through `EntryRepository`). `InitializeDatabase` (`POST /api/Admin/Initialize`) creates the five current containers only; it does not create `MainContainer`. Do not add startup initialization (Section 5).

## 16. Test environment

- A sister test site (`swa-bowlmadness-test`) has its own serverless Cosmos account (`cosmos-bowlmadness-test`). A separate account means a test setting can never reach production data. Resource names and workflows: docs/INFRASTRUCTURE.md.
- `Api/Helpers/TestEnvironment` (`ITestEnvironment.IsEnabled`) is true only when the app setting `SiteEnvironment` is exactly `Test` and the `CosmosClient` endpoint host is exactly the test account's. It is an allow-list, so an unknown or look-alike host is refused as well as the production account. It is registered as a singleton in `Program.cs`.
- `Api/Repositories/TestDataRepository` re-checks the guard before touching Cosmos. It deletes only from Seasons, Picks and PoolArchives (never Players or Configuration) and writes only to those and Configuration.
- `TestDataFunctions`: `ResetTestData` (`POST /api/ResetTestData/{scenario}`) and `GetTestEnvironmentStatus` (`GET /api/TestEnvironment`), both SuperAdmin. `ResetTestData` checks in this order: caller (401 or 403), guard (403), scenario name (400), then deletes and loads, and returns counts. It is not atomic; if it fails part way, run it again.
- Scenarios are JSON fixtures embedded in the Api project (`Api/Scenarios/*.json`, listed in `EmbeddedTestScenarioProvider`): `before-selection-day`, `entry-window` and `mid-season`, with made-up names only. A date is a token such as `{now+240h}`, expanded when the scenario is loaded, so a scenario is never stale. Season ids follow section 2: the season document is partitioned by its year, and pools, games and entries carry the season's id.
- Client: the `/admin/test-data` page and a Dashboard tile, shown only when `GET /api/TestEnvironment` reports the test site.
- Later phases add a last-season replay scenario (Phase 1.1 group C). The score feed mock is described in section 17. `ResetTestData` also sets the score feed mode back to Live.

## 17. Score feed
- `IScoreFeed` (`Api/Services/ScoreFeed`) is the one read-only interface for "games and scores": `GetScoreboardAsync(date?)` and `GetPostseasonGamesAsync(year)`. Types `FeedGame`, `FeedTeam`, `FeedGameStatus` are in `Core/ScoreFeed`.
- Football: `CfbdScoreFeed` wraps `CfbdService`. `GameScoringService` and the `GetExternalGames` and `GetRawCfbdGames` endpoints read through it. Team sync (`SyncTeams`) still uses `ICfbdService`. Hoops still uses `IEspnDataService` and moves onto `IScoreFeed` in Release 3.
- The browser still reads `CfbdGameDto` from `GetRawCfbdGames`; the endpoint converts from `FeedGame` (`CfbdFeedMapping`).
- Switch: the registered `IScoreFeed` is `SwitchingScoreFeed`. It reads the mode from one document, `ScoreFeedSettings` (id and partition key `Config_ScoreFeed`, Configuration container), on every call and forwards to `CfbdScoreFeed` (Live) or `MockScoreFeed` (Mock). A missing or unreadable document means Live. There is no app setting and no test-site condition: it works in production.
- Mock: `MockFeedGame` documents in the Seasons container, partition value `MockFeed`, type `MockFeedGame`, in whichever database the site uses. `ResetTestData` clears them and sets the mode to Live. The mock ignores the requested year so the recorded starting set (`Api/MockFeed/StartingSet.json`, embedded, last season's postseason games) can be linked in any season.
- Admin: `/admin/score-feed` (SuperAdmin) has the Live / Mock toggle, a warning banner while Mock is on, the game editor and the starting-set button. Endpoints `MockFeed/Mode` (get, set), `MockFeed/Games`, `MockFeed/StartingSet`, `MockFeed/ResetPreview`; each checks SuperAdmin first on the server. Switching to Mock is two steps: `GET MockFeed/EnablePreview` (SuperAdmin, read only) lists the linked, not-Final games of the current season whose external id is also in the mock, and `POST MockFeed/Mode` with `mode` Mock is refused with 400 unless the body also has `"confirm":"MOCK"` (exact, case-sensitive); the page asks the admin to type that word. Switching to Live needs no confirmation.
- Reset: there is no stand-alone reset endpoint. `POST MockFeed/Mode` with `{"mode":"Live","resetConfirm":N}` is the only way. The server requires the stored mode to be Mock, requires N to equal the previewed game count, switches to Live first (so no refresh re-applies mock scores), then resets (`MockFeedResetService`, `MockFeedReset`). It applies to the current season's football games that are linked (have an ExternalId) and have a result, whatever the source of the result: it clears scores, sets Scheduled, clears GameDetail, and restores the later-round slot that holds the feeder's winner (matched by team name) to `Winner of {feeder BowlName}`. Slots that do not hold the winner, and later games with a hand-entered result, are left alone and listed. Reads and writes use the season's Id as the partition key, in batches of at most 100; start and finish times and counts are logged. If the reset fails after the switch, the feed is already Live and some games may be reset: switch to Mock again, then switch back to Live with a reset (the page shows the remaining count).

### Season rollover checklist
Each new season:
1. Create the new season (Admin > Seasons). `SeasonId` is the year text (for example "2026"). Mark it current. The score feed year comes from the current season's `SeasonId`; no football score-feed call holds a year literal.
2. Check football pools and games use the new season's `Id` (a GUID) as `seasonId`. Hoops pools may still carry the year text (BUSINESS-RULES section 6).
3. Run Sync Teams (College Football Data) and Sync ESPN Logos for the new team lists.
4. Check the lock date, point values and tiebreaker on each new pool.
5. Make sure the score feed is Live (Score Feed page) before the season starts. On the test site: reset with a scenario, load the starting set, link games, and check scoring before Selection Day.
6. Still to check by hand: the `DateTime.Now.Year` fallback in `PoolSettings.razor` (#126) and hoops season handling (#95).

## 18. Client UI (Release 2)
- **Component library:** MudBlazor, pinned to 9.14.0 (MIT). Its CSS and script come with the package (`_content/MudBlazor/...`); no CDN. Bootstrap (Bootswatch Flatly) and Bootstrap Icons are still loaded from a CDN in `index.html` for pages that have not moved to the new components yet (football pages in Release 2 groups B to D, hoops in Release 3); they are removed when the last such page is gone.
- **Theme and fonts:** `Client/Theme/AppTheme.cs` (MudBlazor theme: navy `#0F2747`, orange `#C2410C`, amber `#F59E0B`, page `#F4F5F7`) and the same values as CSS variables in `wwwroot/css/theme.css`. Atkinson Hyperlegible (body) and Barlow Condensed (headings) are shipped as woff2 files in `wwwroot/fonts` (SIL Open Font License, texts in `LICENSES.txt`).
- **Frame:** `Layout/MainLayout` (providers, disabled-account check, live dot), `Layout/AppHeader` (header, top navigation at 900 px and wider, phone menu, sign-in) and `Ui/BottomNav` (four tabs below 900 px). `PrintLayout` is unchanged. A loading screen in `index.html` shows until Blazor starts (#117).
- **Shared components:** `Client/Ui/`, listed and documented in docs/UI-COMPONENTS.md; styles in `wwwroot/css/ui.css`. The admin page `/admin/components` shows each in every state.
- **Pool switcher state:** `PoolSelection` (scoped service) holds the pools a page offers and the chosen one; the choice is stored in browser local storage under `lastSelectedPoolId`.
- **Logic in Core, with xUnit tests (no bUnit):** `SiteNavigation` (links per site), `StatusLineText` (status line), `TopThreeRule` (the "out" rule, shared by the leaderboard and `BracketSimulator`), `PoolPicker` (which pool shows first). `SignInPathsTests` checks that every sign-in path goes to the chooser.
- **Sign-in:** the chooser is `Pages/Login.razor` (`/login`). The routing file's 401 override and the not-authorized message in `App.razor` go to it; no path goes straight to a provider.
