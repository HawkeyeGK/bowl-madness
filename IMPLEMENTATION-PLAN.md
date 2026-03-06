# NCAA Basketball Expansion — Implementation Plan

**Created:** 2026-03-01
**Status:** Draft — Pending Owner Approval
**Parent Issue:** [#75](https://github.com/HawkeyeGK/bowl-madness/issues/75)

---

## Key Decisions (Agreed During Planning)

These decisions were made during the planning session and govern the entire implementation:

| # | Decision |
|---|---|
| 1 | **No `GameBase` abstract class.** `HoopsGame` is a standalone model. A lightweight `IScorable` interface provides the small surface the scoring engine needs. Football's model stays untouched. |
| 2 | **Single `TournamentRound` enum** (renamed from `PlayoffRound`) with values for both sports. |
| 3 | **Season model unchanged.** No dual active flags. Sport filtering happens at the pool/game `Type` level via `SiteContext`. A season spans both sports; pools are concluded/archived independently per sport. |
| 4 | **`FirstFour` is a distinct round value** in the `TournamentRound` enum, enabling independent point configuration for play-in games. |
| 5 | **`HoopsGame.PointValue` defaults to 0.** Missed hydration is immediately visible rather than silently wrong. |
| 6 | **Football What-If stays untouched.** `WhatIf.razor` and `WhatIfScoringEngine` are not refactored. Basketball gets its own independent implementation. |
| 7 | **SSO deferred.** Users log in separately on each domain. Shared `UserProfile` in Cosmos provides the foundation for future SSO if needed. |
| 8 | **Bracket generation is deterministic.** Admin enters regions + Final Four pairings; system generates all 67 game shells with correct `NextGameId` linking. Admin manually enters first-round teams/seeds. |
| 9 | **Manual results entry is the primary path.** Live score integration is a late-phase enhancement, not a prerequisite. |
| 10 | **Minimal theming differences.** Same layout, styling, and nav structure across both sites. Only branding text changes ("Bowl Madness" vs "Hoops Madness") driven by `SiteContext`. Domain: `hoops-madness.com`. |
| 11 | **Azure configuration changes** (custom domain, API keys) are documented in relevant phases and require explicit owner confirmation before execution. |

---

## Guiding Constraints

- **Football must never break.** Every phase must be deployable with existing football functionality intact.
- **Vertical slices.** Each phase delivers a thin but complete path through the stack (model → API → UI) so problems surface early.
- **Admin tools built in workflow order.** Each admin tool is built and validated in the sequence an admin would actually use it.
- **Accuracy over speed.** Pause and ask for clarification when intent, scope, or approach is ambiguous.

---

## Phase 1 — Multi-Domain & Site Context
**Issue:** [#76](https://github.com/HawkeyeGK/bowl-madness/issues/76)

**Goal:** Both domains serve the app with distinct branding and navigation. Visiting `hoops-madness.com` shows a basketball-branded experience (even if there's no basketball content yet). Football is unaffected.

### Deliverables
- **Azure custom domain:** Add `hoops-madness.com` to the Azure Static Web App. Owner handles DNS configuration at registrar; agent runs `az staticwebapp hostname set` after owner confirmation.
- **`SiteContext` service:** Client-side scoped service that detects the active sport from the hostname (`bowl-madness.com` → Football, `hoops-madness.com` → Basketball). Path-based fallback (`/football/...`, `/basketball/...`) for localhost development.
- **Sport-aware navigation:** `NavMenu.razor` conditionally shows "Bowl Madness" or "Hoops Madness" branding based on `SiteContext`. Nav links filtered by sport context (football pages on football domain, basketball pages on basketball domain). Admin nav will gain basketball sections in later phases as those pages are built.
- **Dev-mode sport toggle:** A mechanism for switching sport context during local development without multiple domains.

### Checkpoint
- Visiting each domain (or using the dev toggle on localhost) shows the correct branding.
- Football domain behaves identically to today.
- Basketball domain shows the correct branding with appropriate empty states.
- Build passes, all existing tests pass.

### Azure Actions (Require Owner Confirmation)
- `az staticwebapp hostname set` to add `hoops-madness.com`
- Owner configures DNS (CNAME or TXT verification) at domain registrar

---

## Phase 2 — Core Foundation
**Issue:** [#77](https://github.com/HawkeyeGK/bowl-madness/issues/77)

**Goal:** All shared infrastructure for basketball exists in the codebase. No new UI beyond Phase 1, but the models, interfaces, and engine updates are in place.

### Deliverables
- **Rename `PlayoffRound` → `TournamentRound`:** Add basketball values (`FirstFour`, `RoundOf64`, `RoundOf32`, `Sweet16`, `Elite8`, `FinalFour`, `NationalChampionship`). Update all existing references throughout the codebase.
- **`IScorable` interface:** The minimal surface the scoring engine needs — `PointValue`, `IsFinal`, `WinningTeamName`, `Round` (as `TournamentRound`), and whatever else `ScoringEngine.Calculate` reads from a game.
- **`BowlGame` implements `IScorable`:** No structural changes to the model; just adds the interface.
- **`HoopsGame` domain model:** Standalone model implementing `IScorable`. Includes basketball-specific fields (`Region`, etc.) and all required Cosmos fields. `PointValue` defaults to 0. Both `[JsonProperty]` and `[JsonPropertyName]` on every property.
- **`HoopsPool` domain model:** Mirrors `BowlPool` structure with `Dictionary<TournamentRound, int>? PointsPerRound` for round-based scoring configuration.
- **`Constants` updates:** Add `HoopsGame` and `HoopsPool` to `DocumentTypes`.
- **`ScoringEngine` update:** Accepts `IScorable` instead of `BowlGame`. Football behavior unchanged — existing tests must still pass.

### Checkpoint
- Build passes with zero warnings related to the refactor.
- All existing football tests pass unchanged.
- New unit tests validate `IScorable` contract for both `BowlGame` and `HoopsGame`.
- `ScoringEngine` works identically for football inputs.

---

## Phase 3 — Basketball Team Data
**Issue:** [#78](https://github.com/HawkeyeGK/bowl-madness/issues/78)

**Goal:** Basketball team data (names, logos, colors, conferences) is available in Cosmos for use by admin tools in later phases.

### Deliverables
- **Basketball API client:** Service for fetching NCAA basketball teams from CollegeBasketballData.com (or equivalent). Register for API key.
- **`Config_Teams_Basketball` document:** Stored in the Configuration container, same pattern as `Config_Teams_FBS`.
- **Admin sync UI:** "Sync Basketball Teams" button on the admin API integration page, analogous to the existing football team sync.
- **`SiteContext`-aware team config endpoint:** API returns the correct team config (FBS or basketball) based on context, or a new endpoint specific to basketball teams.

### Checkpoint
- Basketball teams are synced and stored in Cosmos.
- Admin can trigger a sync from the UI.
- Team data includes logos, colors, and other display info needed for game cards and bracket rendering.

### Azure Actions (Require Owner Confirmation)
- Set basketball API key as an environment variable via `az staticwebapp appsettings set`

---

## Phase 4 — Basketball Pool Creation
**Issue:** [#79](https://github.com/HawkeyeGK/bowl-madness/issues/79)

**Goal:** Admin can create and configure a basketball pool with round-based scoring.

### Deliverables
- **`HoopsPoolRepository`** and supporting API endpoints (CRUD for basketball pools).
- **Basketball pool settings page:** Admin UI to create/edit a `HoopsPool` — name, invite code, lock date, `PointsPerRound` configuration (per-round point values for `FirstFour` through `NationalChampionship`).
- **`SiteContext`-driven pool filtering:** API and client filter pools by document type so football domain shows `BowlPool` documents and basketball domain shows `HoopsPool` documents.
- Basketball pool settings page accessible via basketball admin nav.

### Checkpoint
- Admin can create a basketball pool with round-based scoring config from the basketball domain.
- Football pool functionality unchanged.
- Pool appears in basketball domain pool lists, not on football domain.

---

## Phase 5 — Tournament Configuration & Bracket Generation
**Issue:** [#80](https://github.com/HawkeyeGK/bowl-madness/issues/80)

**Goal:** Admin can define a tournament structure and the system generates all 67 correctly-linked game shells.

### Deliverables
- **`HoopsGameRepository`** and supporting API endpoints (CRUD for basketball games).
- **Bracket generation service:** Given four region names and two Final Four pairings, generates:
  - 4 First Four games (`TournamentRound.FirstFour`)
  - 32 Round of 64 games (`TournamentRound.RoundOf64`), with First Four games' `NextGameId` pointing to the correct Round of 64 slots
  - 16 Round of 32 games, 8 Sweet 16 games, 4 Elite 8 games, 2 Final Four games, 1 National Championship game
  - All games have correct `NextGameId` links, region assignments, round assignments, and seed slot expectations
- **Tournament configuration admin page:** UI to name the four regions, specify which regions pair for each Final Four semifinal, and trigger bracket generation. Includes a confirmation step before generating.
- **Bracket verification view:** A diagnostic visualization (tree or table) that lets the admin confirm every game is correctly wired — shows the full bracket structure with round, region, and `NextGameId` relationships.
- Games are associated with the pool via `HoopsPool.GameIds`.

### Checkpoint
- Admin can configure and generate a complete 67-game bracket.
- Verification view confirms correct structure: 4 First Four → 32 Round of 64 → 16 → 8 → 4 → 2 → 1.
- Every game has the correct `NextGameId`, round, and region.
- Generated games appear only on the basketball domain.

---

## Phase 6 — Team/Seed Assignment
**Issue:** [#81](https://github.com/HawkeyeGK/bowl-madness/issues/81)

**Goal:** Admin can populate the first-round bracket with actual teams and seeds.

### Deliverables
- **Team assignment admin page:** Region-by-region view showing each first-round matchup slot (e.g., "1 seed vs 16 seed"). Admin selects teams from the basketball team config dropdown and assigns seeds.
- **`TeamInfo` denormalization:** When a team is assigned, its `TeamInfo` (logo, colors, etc.) is denormalized into the `HoopsGame` document, same pattern as football.
- **First Four integration:** First Four slots are populated similarly, with the system understanding that their winners feed into specific Round of 64 games.

### Checkpoint
- Admin can assign all 68 teams to their correct bracket positions.
- Each game displays correct team names, seeds, logos, and colors.
- Bracket verification view now shows team names alongside the structural links.

---

## Phase 7 — Pick Entry
**Issue:** [#82](https://github.com/HawkeyeGK/bowl-madness/issues/82)

**Goal:** Players and admins can submit bracket picks through a bracket-style visual interface.

### Deliverables
- **Bracket pick entry component:** Visual bracket UI where users click to select winners round by round. Must support:
  - **Forward propagation:** Selecting a winner in an early round makes that team available in the next round's matchup.
  - **Cascade clearing:** Changing a pick in an early round clears any downstream picks that depended on the changed team.
  - Region-by-region navigation or a full-bracket view (determine during implementation based on screen real estate).
- **Player pick entry page:** Basketball equivalent of `MyPicks.razor` — dashboard showing current entries, join flow with invite code, bracket editor.
- **Admin pick entry page:** Admin override page to create or edit any user's bracket (for phone-in entries, corrections). Same bracket component with admin permissions (no lock date enforcement, can edit any entry).
- **Entry API endpoints:** Create/update/delete basketball entries. Same `BracketEntry` model — `Dictionary<GameId, TeamName>` picks are sport-agnostic.
- **`EntryUpdateValidator` support:** Validation logic works for basketball entries (lock date enforcement, bracket name uniqueness within pool).

### Checkpoint
- Player can join a basketball pool, fill out a bracket, and save picks.
- Changing an early-round pick correctly cascades to clear dependent later-round picks.
- Admin can create and edit entries on behalf of players.
- Picks are persisted and retrievable.
- Football pick entry is unaffected.

---

## Phase 8 — Manual Results Entry & Propagation
**Issue:** [#83](https://github.com/HawkeyeGK/bowl-madness/issues/83)

**Goal:** Admin can enter game results manually and winners propagate through the bracket.

### Deliverables
- **Basketball results entry page:** Admin UI to enter scores and mark games as final, analogous to `GameResults.razor`. Organized by round for easy navigation (First Four first, then Round of 64, etc.).
- **Winner propagation:** When a game is marked final, the winning team's name and `TeamInfo` propagate to the appropriate slot in the `NextGameId` game. Reuses or mirrors the existing `GameScoringService.PropagateWinner` pattern.
- **Basketball scoreboard:** Player-facing scoreboard showing game results, organized by round/region. Live status indicators for in-progress games.
- **Propagation diagnostics:** Admin can verify propagation is correct and force re-propagation if needed, analogous to football's Force Propagation feature.

### Checkpoint
- Admin can enter results for First Four games and see winners propagate to Round of 64.
- Propagation works correctly through all rounds up to the Championship.
- Scoreboard displays results accurately.
- Football results and propagation are unaffected.

---

## Phase 9 — Scoring & Leaderboard
**Issue:** [#84](https://github.com/HawkeyeGK/bowl-madness/issues/84)

**Goal:** Basketball leaderboard calculates and displays standings with round-specific scoring.

### Deliverables
- **`PointsPerRound` hydration:** Before scoring, look up each game's round in the pool's `PointsPerRound` dictionary and write the value into the game's `PointValue` in memory. Guard: flag an error if any basketball game still has `PointValue == 0` after hydration.
- **Leaderboard API endpoint:** Basketball-specific leaderboard calculation using the updated `ScoringEngine` with `IScorable`. Returns round-by-round score breakdowns using basketball round names.
- **Basketball leaderboard page:** Displays rankings with basketball-specific column headers (`First Four`, `Round of 64`, `Round of 32`, `Sweet 16`, `Elite 8`, `Final Four`, `Championship`). Same underlying ranking/sorting logic as football.
- **Max possible score calculation:** Tracks which picked teams are still alive in the bracket.

### Checkpoint
- Leaderboard correctly scores entries against entered results.
- `PointsPerRound` hydration works correctly — different pools with different scoring configs produce different rankings.
- Round-by-round breakdown displays accurately.
- Football leaderboard is unaffected.

---

## Phase 10 — Basketball What-If / Path to Victory
**Issue:** [#85](https://github.com/HawkeyeGK/bowl-madness/issues/85)

**Goal:** Users can simulate remaining tournament outcomes and see how standings would change.

### Deliverables
- **Basketball What-If page:** Independent implementation (does not modify football's `WhatIf.razor` or `WhatIfScoringEngine`). Design TBD during this phase — the football grid approach won't work for 67 games, so an alternative visualization is needed. Options discussed in the requirements doc include:
  - Split-screen: leaderboard on left, interactive bracket on right
  - "Ghosting" to highlight a player's picks on the bracket
  - "Chalk" button to auto-fill remaining games with higher seeds
  - "My Perfect World" button to auto-fill with the user's picks
  - Chaos toggle to flip winners and watch standings change
- **Client-side calculation:** All simulation runs in WASM — no server round-trips. Data payload is small enough (~50 users × 67 picks).
- **Elimination detection:** Flag users who are mathematically eliminated (max possible score < current leader's score).

### Checkpoint
- Users can simulate outcomes and see leaderboard impact.
- Performance is acceptable (no browser freezing with 67 games × 50 entries).
- Football What-If is completely unaffected.

**Note:** The specific visualization approach for this phase should be discussed with the owner before implementation begins, as it is the most design-intensive piece of the expansion.

---

## Phase 11 — Bracket Printing
**Issue:** [#86](https://github.com/HawkeyeGK/bowl-madness/issues/86)

**Goal:** Users can print an empty bracket or a filled bracket with their picks.

### Deliverables
- **Empty bracket print view:** Printable blank bracket showing all matchups, regions, and seeds. Suitable for users who want a paper bracket.
- **Filled bracket print view:** Printable bracket showing a specific user's picks highlighted, analogous to football's `PrintBracket.razor`.
- **Print-optimized layout:** CSS print styles for clean output on standard paper sizes.

### Checkpoint
- Both empty and filled brackets render correctly in print preview.
- Print output is clean and readable.
- Football print functionality is unaffected.

---

## Phase 12 — Basketball Payments
**Issue:** [#87](https://github.com/HawkeyeGK/bowl-madness/issues/87)

**Goal:** Admin can track payment status for basketball pool entries.

### Deliverables
- **Basketball payment manager:** Admin page to mark basketball entries as paid/unpaid, analogous to football's `PaymentManager.razor`. Filtered to basketball pools via `SiteContext`.
- **Reuse existing `IsPaid` field** on `BracketEntry` — no model changes needed.

### Checkpoint
- Admin can view and toggle payment status for basketball entries.
- Football payment management is unaffected.

---

## Phase 13 — Basketball Archives
**Issue:** [#88](https://github.com/HawkeyeGK/bowl-madness/issues/88)

**Goal:** Concluded basketball pools can be archived and viewed historically.

### Deliverables
- **Archive support for basketball pools:** `ArchivePool` endpoint works with `HoopsPool` and `HoopsGame` documents. Snapshots games and scored standings into a `PoolArchive` document.
- **Basketball archive viewer:** Archive list and detail pages filtered by sport context.
- **Archive flow:** Conclude basketball pool → archive → historical view available.

### Checkpoint
- A concluded basketball pool can be archived successfully.
- Archived basketball pool is viewable with correct standings and game results.
- Football archives are unaffected.

---

## Phase 14 — Live Score Integration
**Issue:** [#89](https://github.com/HawkeyeGK/bowl-madness/issues/89)

**Goal:** Basketball game scores update automatically from the external API during the tournament.

### Deliverables
- **Basketball score polling:** Extend or mirror `GameScoringService.CheckAndRefreshScoresAsync` for basketball games. Match games by `ExternalId`, update scores and status, trigger propagation on completion.
- **Game linking admin tool:** Basketball equivalent of `GameLinker.razor` — admin links generated game shells to external API game IDs once the API has matchup data available.
- **`ApiHomeTeam`/`ApiAwayTeam` bridge fields** on `HoopsGame` for handling any team name mismatches between the app and the external API.

### Checkpoint
- Scores update automatically when games are in progress.
- Status transitions (Scheduled → InProgress → Final) work correctly.
- Winner propagation fires on game completion.
- Football score integration is unaffected.

**Note:** This phase depends on the basketball API having live tournament data available, which may not happen until the tournament begins.

---

## Phase 15 — Final Integration & Launch
**Issue:** [#90](https://github.com/HawkeyeGK/bowl-madness/issues/90)

**Goal:** Both sites are fully functional and ready for production use.

### Deliverables
- **End-to-end testing:** Full user journey on both domains — create pool, generate bracket, enter teams, make picks, enter results, view leaderboard, simulate outcomes, print bracket, manage payments, archive.
- **Cross-domain verification:** Confirm complete data isolation — basketball data never appears on football domain and vice versa.
- **Performance testing:** Bracket rendering, pick entry cascade, What-If simulation, and leaderboard calculation all perform acceptably.
- **Edge case review:** First Four propagation, tiebreaker handling, lock date enforcement, admin overrides.
- **Any remaining polish** identified during prior phase checkpoints.

### Checkpoint
- Both sites fully functional with no cross-contamination.
- Owner has validated the complete basketball workflow end-to-end.
- Ready to deploy for the tournament.

---

## Workflow Notes

- **Track B applies to all phases.** Each phase follows: Implement → Build Validator → QA Agent → Code Review Agent → Owner Checkpoint → Commit.
- **Feature branch:** `feature/basketball-expansion` — all work happens here until the owner is ready to deploy.
- **Commits reference the phase issue** (e.g., `#76` for Phase 1 work).
- **No pushes to `main`** without explicit owner instruction.
