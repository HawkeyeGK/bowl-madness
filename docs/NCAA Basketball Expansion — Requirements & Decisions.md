# NCAA Basketball Expansion — Requirements & Decisions

## How to Use This Document

This document is the primary input for planning and implementing a major expansion to an existing college football bowl pool web application. It was prepared by the project owner with the help of Claude (claude.ai) after two earlier planning sessions conducted in Gemini.

**Your job is to:**
1. **Read this document thoroughly** to understand the requirements, decisions, and design context.
2. **Familiarize yourself with the actual codebase** before making any implementation decisions — several design proposals in this document were made without access to the code and may need adjustment.
3. **Develop a phased implementation plan** with logical, deployable checkpoints where the owner can validate progress and course-correct. Do not simply adopt the prior roadmap in Section 8 — use it as reference and improve upon it.
4. **Distinguish between requirements and design proposals.** Requirements (labeled "Logical Requirements" in each section) are firm. Design proposals (labeled "Design Proposals") are suggestions from the Gemini sessions that should be evaluated against the codebase and may be replaced with better approaches.
5. **Ask clarifying questions** if anything is ambiguous or if you discover codebase realities that conflict with assumptions in this document.

**Key constraint:** Existing football functionality must not break at any point during development. The football site is in production with real users.

**Source:** Extracted and refined from two Gemini planning sessions (Jan 30, 2026 & Feb 2, 2026), then reviewed and corrected by the project owner.
**Current Stack:** .NET 8 Blazor WASM (Client) + Azure Functions Isolated (API) + Cosmos DB (NoSQL)
**Goal:** Expand the existing College Football Bowl Pool application to support an NCAA Basketball Tournament (March Madness) pool.

---

## 1. Architectural Decisions (Confirmed by Owner)

### 1.1 The "Sister Site" Strategy
- **Single codebase** (monorepo) deployed to one Azure Static Web App, but serving two contexts.
- Basketball will use a **completely separate domain** (not a subdomain of bowl-madness.com). The football domain is football-specific, so a new domain is needed for basketball.
- Users **do not need to know** both sites share infrastructure. They should **feel like** they are visiting a different site with the same theme and usage patterns.
- **Single sign-on is a nice-to-have:** Ideally, a user authenticated on one domain would not need to log in again on the other. However, this is not a hard requirement — if cross-domain SSO proves too complex given the auth setup, requiring separate logins is acceptable.
  - **Technical note:** Cross-domain cookie sharing is non-trivial (cookies are scoped to their domain). The implementation session should evaluate the current auth mechanism and determine if SSO is feasible without significant effort.
- Under the hood, **User records in Cosmos are shared** — same person, same account across both domains.

### 1.2 Routing & Domain Strategy
- **Two custom domains → one Azure Static Web App → hostname-based SiteContext internally.**
- Azure SWA natively supports multiple custom domains pointing to the same deployment.
- The `SiteContext` service determines the active sport primarily by checking the **hostname** (via JS interop or `NavigationManager.BaseUri`):
  - `bowl-madness.com` (or equivalent) → Football mode
  - `bracket-madness.com` (or equivalent basketball domain) → Basketball mode
- **Fallback for development:** Path-based prefixes (`/football/...`, `/basketball/...`) can be used on `localhost` so both modes are testable without multiple domains.
- The SiteContext drives everything downstream: theming/branding, navigation, season flag filtering, game/pool type loading.
- This approach maximizes code reuse while giving each sport a distinct branded identity.

### 1.3 Admin Strategy
- **Universal Admin** — a single SuperAdmin role manages both sports. No need for FootballAdmin vs. BasketballAdmin separation.

### 1.4 Shared Seasons
- **Reuse the Season record** (e.g., `SeasonId: "2025"`, Name: "2025-2026 Season").
- Seasons overlap: Football ends in January, Basketball ends in April.
- **Refactor:** Replace the single `IsCurrent` boolean with two granular flags:
  - `IsFootballActive`
  - `IsBasketballActive`
  - Both can be `true` simultaneously.
- **Existing pattern (no change needed):** Season documents have a `SeasonId` that is a simple year (e.g., `"2025"`, `"2026"`) and a display `Name` that already follows a multi-year convention (e.g., "2025-2026 Season"). There is existing data following this pattern. No renaming or restructuring of season IDs is required.
- `SeasonService.GetCurrentSeasonAsync` must check `SiteContext` and return the season where the appropriate flag is active.

### 1.5 Data Isolation
- **Shared:** Users, Seasons (via shared Partition Key)
- **Isolated:** Games and Pools are distinct document types (`BowlGame` vs. `HoopsGame`, `BowlPool` vs. `HoopsPool`) to prevent cross-sport data pollution.
- API queries must filter by document type to avoid football leaderboards accidentally pulling basketball games.

---

## 2. Data Model Requirements

### 2.1 Logical Requirements
- A basketball game must capture: the two teams playing, their seeds, the region of the bracket it belongs to, which round of the tournament it is, the score/result, and how it connects to the next round.
- The modern NCAA tournament has **68 teams** with **four play-in games** (the "First Four"). The bracket must support these play-in games, which feed into specific slots in the Round of 64. This results in **67 total games** (4 play-in + 63 main bracket).
- The system must track parent-child relationships between games (each game's winner feeds into a subsequent game).
- Basketball games are all played on neutral sites — "home" and "away" are not meaningful concepts, just slot labels.
- Basketball games and football games must be cleanly isolated so queries for one sport never accidentally return data from the other.
- The system needs to represent the six rounds of March Madness: Round of 64, Round of 32, Sweet 16, Elite 8, Final Four, National Championship.
- A basketball pool is a grouping of games with its own configuration (scoring rules, etc.), analogous to a football bowl pool.
- Existing football data and behavior must not be broken by any model changes.

### 2.2 Design Proposals (From Gemini Sessions — Validate Against Codebase)
The following design ideas emerged during planning. They should be evaluated during the implementation session rather than treated as firm requirements:

- **Separate document types:** `HoopsGame` and `HoopsPool` as distinct types from `BowlGame` and `BowlPool`, mirroring the same structure but with basketball-specific fields (`Region`, `Round`, `SeedHome`, `SeedAway`).
- **Shared abstract base class (`GameBase`):** Extract common properties from `BowlGame` so a single scoring engine can process both sport types polymorphically.
- **Bracket linking via `NextGameId`:** Reuse the existing playoff game linking pattern (already in `BowlGame`) to define the bracket tree structure.
- **Play-in games ("First Four"):** The Gemini sessions assumed a 64-team / 63-game bracket. The actual tournament has 68 teams and 4 play-in games (67 total games). How to model the First Four and integrate them into the bracket structure was **not discussed** and needs to be addressed during implementation planning.
- **Deterministic slot assignment:** Rather than storing which slot (top/bottom) a feeder game maps to, use a convention: lower feeder Game ID = top slot, higher = bottom slot. This avoids adding fields to the DB but shifts logic to the UI/service layer.
- **No visual positioning in DB:** Don't store grid locations or sort orders — let the UI derive bracket layout from the game links.
- **Expand `PlayoffRound` enum:** Current values include `Standard`, `Round1`, `QuarterFinal`, `SemiFinal`, `Championship`. Basketball needs: `RoundOf64`, `RoundOf32`, `Sweet16`, `Elite8`, `Final4`, `NationalChampionship`.
- **`Clone()` method on game objects:** Needed for the Simulation Bridge pattern (Path to Victory) to create isolated copies for what-if calculations.

---

## 3. Scoring Requirements

### 3.1 Pool-Level Point Configuration (PointsPerRound)
- **Decision:** Move point-value-per-round definition from the Game to the Pool.
- Add `public Dictionary<PlayoffRound, int>? PointsPerRound` to both `BowlPool` and `HoopsPool`.
- Example basketball config: `{ RoundOf64: 1, RoundOf32: 2, Sweet16: 4, Elite8: 8, Final4: 16, NationalChampionship: 32 }`
- **Allows different pools to have different scoring** for the same tournament (e.g., standard vs. fibonacci).
- The owner confirmed that even for football, points were assigned per round in practice (all regular bowls = 1pt, first round playoff = 3pts, etc.), so this pattern works universally.

### 3.2 Logical Requirements
- Point values for basketball should be defined per round, not per individual game. An admin should not have to manually set point values on 67 games.
- Different pools should be able to use different scoring rules for the same tournament (e.g., standard vs. fibonacci).
- The scoring engine must be able to calculate scores for both football and basketball.
- Existing football scoring behavior must not break.
- Existing code that reads `game.PointValue` (leaderboard, Path to Victory, GameCard UI, etc.) should continue to work without widespread changes.

### 3.3 Design Proposals (From Gemini Sessions — Validate Against Codebase)
- **Pool-level `PointsPerRound` dictionary:** Store scoring config on the Pool rather than the Game. Football currently stores `PointValue` on each game, but in practice values are assigned by round anyway.
- **Hydration pattern:** At runtime, pre-process games by looking up their round in the pool's `PointsPerRound` dictionary and writing the value into `game.PointValue` in memory. This lets all existing consumers read `PointValue` as they always have, without knowing the source changed. Falls back to the DB-stored value if no pool config exists (backward compatible for football).
- **Universal Scoring Engine:** Refactor `ScoringEngine.Calculate` to accept a polymorphic game type (e.g., `IEnumerable<GameBase>`) so the same engine handles both sports.

---

## 4. UI Requirements

### 4.1 Logical Requirements
- The basketball site should present a **bracket-style visual** for pick entry — simple lists (like the football bowl pick UI) will not work for a tournament structure. Users need to see and interact with the bracket as a tree.
- **Forward propagation:** Winners selected in earlier rounds must appear as available/selected in later rounds, similar to how the app currently works for picking playoff football games.
- **Cascade clearing:** If a user changes their pick in an early round, that change must propagate to later rounds. Even if later rounds were previously filled out, any picks that are logically invalidated by the change must be cleared so the user can make necessary corrections.
- The basketball leaderboard should display round-specific columns (e.g., "Round of 64", "Sweet 16") rather than the football-specific column labels. The underlying ranking/sorting logic is the same for both sports.
- The app must know which sport context it's in (driven by the domain the user is visiting) and show the appropriate UI, navigation, and data for that sport.
- Admin functionality needs to cover both sports: pool settings, game/bracket creation, results entry, and payments. Sport-specific admin pages are fine where the workflows differ (e.g., defining a football bowl pool vs. creating a basketball bracket are fundamentally different tasks). Universal functionality like marking entries as paid can be shared.
- Entering 67 basketball games and their relationships manually would be impractical. The admin experience for creating and managing the tournament bracket should have a bracket-like feel so the cognitive load is manageable. The admin will likely need to manually enter teams and seeds and link them to the external API, similar to how it works for the football pool today. The external API may not have tournament matchups available until after teams are announced, and later-round games may not appear until the participating teams are determined (this was the pattern for football playoffs).

### 4.2 Design Proposals (From Gemini Sessions — Validate Against Codebase)
- **Hybrid component strategy:** Reuse page containers (`MyPicks.razor`, `Leaderboard.razor`) for shared logic (auth, state, loading) and swap child components based on sport context (e.g., `BowlPickEditor` vs. `TournamentBracketEditor`).
- **SiteContext service:** A client-side scoped service (`ISiteContext`) that detects the hostname to determine the active sport, with a path-prefix fallback for local development. Drives navigation, theming, season filtering, and data loading.
- **Navigation splitting:** Wrap football and basketball nav links in conditionals based on the SiteContext, with a dev-mode sport toggle.
- **Admin dashboard split:** Three sections — Site Admin (global: users, seasons, API, backups, maintenance), Football Admin (payments, pool settings, games, results), Basketball Admin (pool settings, bracket creation, results, payments).

---

## 5. Path to Victory / What-If Simulator

### 5.1 Logical Requirements
- The existing football Path to Victory page (`WhatIf.razor` and `WhatIfScoringEngine.cs`) **must not be deleted or broken** during development of the basketball equivalent. It took significant effort to build and test. A new page should be created alongside it; the old one stays in production until the replacement is fully validated.
- The existing football grid interface is valued — the big grid provides an excellent overview of winners, upsets, etc.
- Basketball needs its own What-If / Path to Victory experience. Given the number of games (67), a 67-column grid like football uses would be unreadable. The basketball simulator needs a different visual approach.
- The What-If tool for both sports needs to answer the same core user questions: "Can I still win?", "What happens if team X wins?", and "Where do I stand under different scenarios?"

### 5.2 Design Proposals (From Gemini Sessions — Validate Against Codebase)

**Simulation Bridge pattern:** Rather than maintaining a separate `WhatIfScoringEngine` with custom logic, feed the standard `ScoringEngine` with cloned game objects where user overrides are applied as "final" results. This means the What-If page automatically inherits any features added to the real scoring engine (tiebreakers, round-by-round breakdown, etc.). Build as `PathToVictoryV2.razor` for football first to prove it out.

**Pre-computed lookup tables:** The current `GetAllOriginalTeams` recursively crawls the game tree on every click. This works for football's shallow depth but would freeze the browser for basketball (6 levels deep). Proposed fix: build a dependency dictionary on page load so downstream clearing and team eligibility checks are O(1) lookups instead of recursive traversals.

**Basketball simulator visualization (strongly preferred by owner):** This design addresses the hard problem of conveying the same insights as the football grid in a bracket context:
- **Split-screen:** Leaderboard on left, Visual Bracket on right.
- **"Ghosting":** Hover over a player in the leaderboard → the bracket highlights their specific picks (green = correct/alive, red = eliminated, dotted = future predictions).
- **"Consensus":** Click a game node → popup shows who picked each team and how many.
- **"Chalk" Button:** Auto-fill remaining games with higher seeds (expected result).
- **"My Perfect World" Button:** Auto-fill simulation to match the user's picks (if I get everything right, where do I finish?).
- **Chaos Toggle:** Click a game to flip the winner, watch leaderboard positions change instantly.

**Max potential / elimination check:** An O(N) calculation that checks whether a user's picked teams are still alive to determine if they can still win. If a user's max possible score is less than the leader's current score, they are mathematically eliminated. Runs client-side in WASM.

---

## 6. External API Integration

### 6.1 Basketball Data Source
- Owner wants something similar to `CfbdService` (CollegeFootballData.com) for basketball.
- Target: CollegeBasketballData.com (by the same developer as CFBD).
- Requires a free API key registration.
- Create `ISportDataProvider` interface:
  - `CfbdService` implements it for Football.
  - New service (e.g., `CbbdService`) implements it for Basketball.
- The `GameFunctions` trigger selects the correct service based on the active Season's sport.

### 6.2 Required API Capabilities
- Fetch NCAA basketball teams and seeds.
- Fetch live game scores (for automated results).

---

## 7. Feature Parity Checklist (Basketball Must-Haves)

Each of these exists for football and needs a basketball equivalent:

1. Bracket Printing — Empty (printable blank bracket)
2. Bracket Printing — Filled (user's picks)
3. Scoreboard component (show hoops games)
4. API Scores Integration (automated live scoring)
5. Path to Victory / What-If Simulator
6. Payments management

---

## 8. Implementation Planning Guidance

### 8.1 Instructions for Claude
The implementation plan should be developed by Claude after reviewing the actual codebase. The plan should:
- Be organized into **logical phases with deployable checkpoints** — each phase should result in something that can be deployed and validated before moving on.
- Allow for **course correction** between phases based on what is learned during implementation.
- Respect the constraint that **existing football functionality must not break** at any phase.
- Account for the actual structure of the codebase rather than assumptions made during these planning sessions.

### 8.2 Prior Roadmap (Reference Only — Not Approved)
The following phased plan was discussed during the Gemini sessions. It represents one reasonable ordering but should be iterated upon, not followed blindly:

**Phase 1 — Core Foundation & Universal Scoring:**
Season model refactor (active flags), GameBase abstraction, Universal Scoring Engine, SiteContext service, Path to Victory V2 as football proof-of-concept.

**Phase 2 — Admin & Configuration:**
Admin dashboard updates, pool-level point configuration (PointsPerRound), scoring hydration.

**Phase 3 — Basketball Data & Integration:**
HoopsGame/HoopsPool models, PlayoffRound enum expansion, external API integration, basketball admin bracket creation tool.

**Phase 4 — User Experience:**
Tournament bracket visual component, pick entry, leaderboard updates, hybrid page logic.

**Phase 5 — Operations & Simulation:**
Basketball results entry, tournament simulator (Path to Victory for basketball), feature parity (bracket printing, scoreboard, payments).

---

## 9. Key Technical Constraints & Notes

- **Cosmos DB:** Partition Key for games is `/id`. All `[JsonProperty]` attributes on `BowlGame` must be preserved during refactoring.
- **No breaking changes to football:** Existing bowl pool functionality must continue working throughout the expansion.
- **Client-side computation:** Blazor WASM can handle re-scoring 50+ brackets against simulated reality in milliseconds — prefer client-side calculation over server round-trips.
- **Data payload:** ~50 users × 63 picks ≈ 10KB — small enough to download all pool entries to the client for simulation.
- **The Simulation Bridge replaces WhatIfScoringEngine** but the old code stays until the new implementation is validated.