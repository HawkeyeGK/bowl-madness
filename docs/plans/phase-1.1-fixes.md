# Phase 1.1: fixes before the new UI

Status: waits for Phase 1.0. Written 2026-10-29. Part of docs/UPGRADE-PLAN-2026.md (sections 3, 12 and 13). One release, built as four groups; run each group in its own session, in the order below.

## Start here (for a new session)
1. Read CLAUDE.md, docs/BUSINESS-RULES.md, docs/UPGRADE-PLAN-2026.md sections 3, 12 and 13, then this file.
2. Confirm Phases 0 and 1.0 are done (their "Progress" sections). If not, stop and tell the owner.
3. Read every issue in your group, including comments (`gh issue view <n> --comments`). Several were widened by comments after filing.
4. Find your group's "Progress" list below and continue from the first open item.

## Goal
Ship the security, data and entry-window fixes the bowl season needs, and put Path to Victory on one tested bracket simulator, with the current UI. Target: production about 2026-11-20, well before Selection Day (2026-12-06) and the entry window (about Dec 6-12).

## Rules for every group
- Tests first: write a test that fails on today's code, then fix (CLAUDE.md "Bugs"). Tests that Phase 0 labelled `KnownFailure` for an issue lose the label when that issue is fixed.
- Run security-data-review as well as code-review for groups B, C and D (they touch authorization, data writes, scoring or picks).
- Verify on the test site with the Phase 0 scenarios before asking to merge. Never test writes against production.
- `Closes #N` for each issue fully fixed; `Refs #N` otherwise.
- Comment on an issue when its scope changes; propose BUSINESS-RULES edits rather than making them.

## Must ship if time runs short
Group A, group B, #129, #128 and #130. If the date is at risk, the rest of group C (the full simulator and #136, #109, #127, #94) moves to Release 2; the owner decides.

---

## Group A: score feed interface, mock, and season setup
**Why first:** every later group needs pools and live-looking scores on the test site, and creating a pool crashes today (#129).

**Issues:** #102 (hard-coded 2025 season in the College Football Data calls), #129 (New Pool crash).

**Decided (plan section 13):** keep College Football Data for football this season. One internal score-feed interface ("games and their scores") for both sports; College Football Data and ESPN as implementations; one mock. Football moves onto it now; hoops in Release 3.

**Work:**
1. Design the interface from what `GameScoringService` and `HoopsGameScoringService` need (status, period, clock, scores, team names, external id), plus the postseason game list used for import and linking. Show the owner the shape before building (new pattern, already approved in principle).
2. Football implementation wraps `CfbdService`. #102: the season year comes from the current season, not 2025 (`GameFunctions.cs:69,94` and the CFBD calls).
3. Mock implementation, used when `ScoreFeed=Mock` and the Phase 0 test guard passes. It reads and writes its data in the test database.
4. Admin "Score feed" page (test site only): list games; set scheduled, in progress (quarter, clock) or final with scores; change the team names the feed reports; load a starting set.
5. Starting set: a one-time recording of last season's real postseason games. Get it read-only from production's SuperAdmin GET endpoint `GetRawCfbdGames` (it still uses 2025) through the owner's signed-in browser, save it as a fixture.
6. #129: New Pool takes the year from the current season's `SeasonId`.
7. Tests: the scoring service against the mock (status changes, scores, a final with unmatched names, which sets up #133 in group B).

**Progress:** [ ] interface agreed [ ] CFBD implementation + #102 [ ] mock [ ] admin page [ ] starting set [ ] #129 [ ] tests [ ] test site check [ ] merged

---

## Group B: security and data
**Issues:** #93, #97, #98, #99, #131, #133, #134.

**Notes:**
- #97 is wider than its title (see its comment): redact `picks`, `auditLog` and `userId` for non-owners before lock, and `auditLog` and `userId` for non-admins always; require season and pool on list endpoints; hide tiebreakers on the leaderboard before lock; cover the backup.
- #93 includes `SaveSeason` (anonymous can change the current season), `GetBackupData`, `SyncTeams`.
- #99 and #134 are one change: the server builds the saved entry from allowed fields only.
- #131: an existing entry keeps its stored pool; a non-admin save with another `poolId` is refused.
- #133: no Final without two scores and a winner, from the feed or the admin page; the scoring engine treats a Final game with no winner as undecided.
- #98: a disabled user is treated as signed out on every endpoint.
- Each fix flips its Phase 0 `KnownFailure` tests.

**Progress:** [ ] #93 [ ] #97 [ ] #98 [ ] #99 + #134 [ ] #131 [ ] #133 [ ] reviews [ ] test site check [ ] merged

---

## Group C: bracket core and Path to Victory
**Issues:** #130 (corrected result overwrites a seed), #136 (ranks), #109 (elimination), #127 (simulated Max), #94 (hoops what-if tests).

**Design (plan section 12, decided):**
- One class in Core. Start time sets the order everywhere. One shared slot rule: if one slot already has a team, the winner fills the empty slot; otherwise the earlier kickoff goes to home. No new field on games.
- The simulator returns each game's teams and winner (real or chosen), each entry's score, Max, rank and whether it can still finish top 3, and each pick's state (won, lost, dead, open). Ranking reuses `ScoringEngine`'s order.
- It reports any playoff game that starts before a game that feeds it.
- `WhatIf.razor` keeps its look but only draws the simulator's output. Server propagation (`GameScoringService.PropagateWinner`), `ScoreboardCard`, `PrintBracket` and `Admin/GameResults` use the shared slot rule.

**Work:**
1. Characterization tests of today's `WhatIf.razor` behavior (cases from `ComputeGameState`), so the new class can be checked against them.
2. Slot rule + #130 regression tests (correct a first-round result; set a final back to scheduled).
3. Simulator + #136, #109, #127 tests; rule checks over many generated seasons (score never above Max; no chosen winners equals the leaderboard exactly; all decided means Max equals Score; a team that lost never adds to Max; choosing the real result changes nothing).
4. Last-season replay (approved by the owner): read-only export of last season's archived pools, player names replaced, saved as a fixture and a `last-season-replay` scenario; replay in start-time order and compare final points, correct picks and ranks with the archive. Differences are checked by hand against what was paid out.
5. #94: find out whether the tests or the code are wrong; fix; remove the `KnownFailure` label.
6. Switch the pages and the server to the shared code.

**Progress:** [ ] characterization tests [ ] slot rule + #130 [ ] simulator + #136 #109 #127 [ ] replay [ ] #94 [ ] pages and server switched [ ] reviews [ ] test site check [ ] merged

**Decision needed from the owner (before step 5, #94):** Phase 0 added 28 new `KnownFailure` tests beside the two #94 tests (30 in all; CI skips them). Decide whether to fix any of the new ones together with #94 in this group, or leave them for the phase that touches that code anyway. Where they stand today:
- 20 are already owned by group B, which flips them with its fixes: #93 (4), #98 (2), and #97, #99, #131 (14, football and hoops).
- 7 are the malformed `x-ms-client-principal` header tests (`SecurityHelperMalformedHeaderTests`; no issue yet). They fit group B, since #98 changes the same helper (`SecurityHelper`).
- 1 is the `UserProfile` attribute test (`ModelSerializationTests`; no issue yet): `Email`, `DisplayName` and `CreatedOn` lack `[JsonProperty]`. A natural time is whenever `UserProfile` or the Users page is next changed.
- 2 are the #94 tests (this group).
Either way, file issues for the two "no issue yet" items first so each label names a real issue, and remove each `KnownFailure` trait in the PR that fixes it.

---

## Group D: entry window
**Issues:** #128 (junk and stale picks), #137 (save incomplete), #132 (rebuild Move Entry), #135 (invite code picks last year's pool), #107 (Central time, widened by comment).

**Notes:**
- #128 uses group C's slot rule: the server rejects picks for games outside the pool and picks of a team that can't reach that game given the entry's own earlier picks; the pages disable unresolved slots and clear dependent picks with a visible notice. Applies to the player page and the admin entry form.
- #137: page-only; the server already allows incomplete saves. The counter counts valid picks only.
- #132: Admin and SuperAdmin; same-season target pool; name check in the target pool; audit log line; the modal lists same-season, non-archived pools; reload after the move. Pairs with #131 from group B.
- #135: decided, use the current season's pool when a code matches more than one.
- #107: one display helper that always returns Central with "CT"; apply it everywhere listed in the issue comment.

**Progress:** [ ] #128 [ ] #137 [ ] #132 [ ] #135 [ ] #107 [ ] reviews [ ] test site check [ ] merged

---

## Release
When all four groups are merged (into `staging` if Phase 0 chose that flow), run the whole Phase 0 and 1.0 checklists on the test site again with the `entry-window`, `mid-season` and `last-season-replay` scenarios, then ask the owner to release to production. After release: read-only smoke test and log check (docs/INFRASTRUCTURE.md "Reading the logs"). Update docs/UPGRADE-PLAN-2026.md, ARCHITECTURAL_CONTEXT.md (simulator, score-feed interface, mock) and propose BUSINESS-RULES edits where behavior was clarified.
