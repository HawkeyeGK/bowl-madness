# Upgrade Plan: 2026 Bowl Season

Draft, 2026-10-28. Status: three-release plan approved 2026-10-28; other items still for discussion.
Smaller releases inside each milestone are fine when they help.

## 1. Goals and constraints

- A working, upgraded Bowl Madness site before bowl matchups are announced (Selection Day, Sunday 2026-12-06).
- As few changes as possible to the bowl side during bowl season (2026-12-06 to the championship, 2027-01-25).
- Hoops upgrades can follow after bowl season, finished before Selection Sunday in March 2027.
- Stay on the Azure free tier. Component libraries must be free.
- Users skew older and less technical; several are over 80. About half use phones.
- More than half of last year's entries came in on paper and were keyed in by admins.
- Players must not be required to sign in. Many only want the standings.
- Standings are what most people look at. A smaller group uses "what if" guessing.
- Letting friends run their own pools is next year. This release must not block it.

## 2. Calendar

| Date | Event |
|---|---|
| 2026-11-10 | .NET 8 and .NET 9 support ends on Static Web Apps managed functions. .NET 10 isolated is supported. |
| ~2026-11-03 | Phase 0 done: test site and test baseline |
| before 2026-11-10 | Release 1.0 (.NET 10) in production (merge target 2026-11-06) |
| ~2026-11-20 | Release 1.1 (fixes) in production (target) |
| ~2026-11-29 | Release 2 in production (target). Bowl side frozen after this. |
| 2026-12-06 | Selection Day. Admin loads games, opens pools, prints blank brackets. |
| ~2026-12-12 | First bowl game; pools lock. Entry window is about six days. |
| 2027-01-25 | Championship game |
| Jan-Feb 2027 | Release 3: hoops |
| mid-March 2027 | Hoops Selection Sunday |

## 3. Release plan

### Before Release 1: code review and test coverage
Owner request, 2026-10-28. Code review done 2026-10-28 (server, client, security, coverage); findings filed as #128-#140 with placements approved by the owner, and scope notes added to #93, #97, #101, #107.

Coverage at review time: 412 tests, 2 failing (#94). Core 71% of lines (scoring engines and validators 97-100%); Api 8% (no tests for entry, pool, game, leaderboard, season functions or winner propagation); Client 2%.

Tests to add before 1.0 (about two days):
- Save-and-reload tests for every Cosmos model with both JSON libraries (the main .NET 10 safety net).
- Sign-in and role tests for every write endpoint: anonymous refused, player refused, admin allowed. Today's known gaps are written as tests marked with their issue so the 1.1 fixes flip them.
- Run the test suite in the deploy workflow.

Test environment and score feed mock: decided, see section 13. Execution plan: docs/plans/phase-0-test-environment.md.

**Phase 0 done 2026-11-22:** the test site (swa-bowlmadness-test, its own database), the test guard with `ResetTestData` and three scenarios, tests gating every deploy, and the baseline serialization and endpoint tests are on production's `main` (PR 147). 30 tests are labelled `KnownFailure` and skipped by CI until their issues are fixed. The score feed mock is still Phase 1.1 group A.

**Phase 1.0 done 2026-11-22:** Core and Api on net10.0, `platform.apiRuntime` set to `dotnet-isolated:10.0`, in production on `main` (PRs 149 and 151). No package changes; the `Azure.Functions.Sdk` switch is a separate follow-up. It reached production after the planned 2026-11-10 date; .NET 8 deploys were still accepted.

**Phase 1.1 done 2026-11-23:** all four groups in production on `main` (group A in PR 158; groups B, C and D in PR 167, merge `bd7c70c`).
- Group A: the score feed interface and the in-app mock switch.
- Group B: security and data fixes.
- Group C: the shared slot rule and the Path to Victory simulator.
- Group D: entry-window fixes.

Releases go through `staging`: feature PRs deploy the test site, and a release PR ships production. The `last-season-replay` test-site scenario was not built; a fixture test compares last season's archive instead. Left open: #130 (no hoops regression test), #153 (the only test still labelled KnownFailure), and the hoops twins #162 and #164 (Release 3).

### Release 1: platform and fixes, current UI
Purpose: a supported, correct site that can carry the bowl season by itself if Release 2 slips. Two releases, approved 2026-10-28; 1.1 is not split further.

**1.0: .NET 10 upgrade only (before 2026-11-10).**
- Move Api and Core from net8.0 to net10.0 (Client and Tests are already net10.0). Set `platform.apiRuntime` to `dotnet-isolated:10.0` in `staticwebapp.config.json`; the file names no runtime today. Update the Functions worker packages.
- Checks: build, full test suite (no new failures beyond the tests labelled KnownFailure), and a full click-through on the test site (Phase 0). Details: docs/plans/phase-1.0-dotnet10.md.

**1.1: fixes before the new UI (~2026-11-20).** One release, four groups, each its own pull request and session, in this order. Details: docs/plans/phase-1.1-fixes.md.
- A, feed and season setup: shared score-feed interface and mock (section 13), #102 hard-coded 2025 season, #129 New Pool crash. First, because every later group needs pools and scores on the test site.
- B, security and data: #93, #97 (scope widened: emails and audit log, leaderboard tiebreakers, backup), #98, #99, #131 players moving entries between pools, #133 Final games with no scores, #134 unchecked entry fields.
- C, bracket core and Path to Victory on the shared simulator (section 12), page unchanged: #130 corrected result overwrites a seed (shared slot rule), #136 rank mismatch, #109 elimination, #127 simulated Max, #94, archive replay test.
- D, entry window: #128 junk and stale picks (uses C's slot rule), #137 incomplete entries, #132 rebuild Move Entry, #135 invite code matching last year's pool, #107 Central time (scope widened).
- Each group starts with tests that fail on today's code.
- If time runs short, groups A and B, #128 and #130 must ship; the rest of group C can move to Release 2.

Execution plans for each phase: docs/plans/README.md.

Out of Release 1: #120, #117, #106, #101, #105, #103, #104, #108, #55, #54, #34, #96. Section 6 says when each fits.

### Release 2: new UI, football player pages and season admin (target ~2026-11-29)
- New component library and theme (section 4), new layout and navigation.
- Player pages: Leaderboard, Scoreboard, My Picks / entry editor, Path to Victory, Entry details, History, Print.
- Admin pages needed for the season: pools, games and results, entries (with fast paper entry), payments.
- New Path to Victory screen on the simulator (section 12). Scoreboard, print pages and admin results switch to the simulator's slot logic.
- Hoops pages keep working on the new layout but are not redesigned.

### Release 3: hoops (Jan-Feb 2027)
- Redesign hoops pages on the new components. #103 hoops tiebreaker, #105 First Four scoring, #104 admin rights.

## 4. Technology

### Stay on Blazor WebAssembly
Recommended. The owner knows it, the domain logic is shared with the API through Core, and the deadline leaves no room for a framework change. The weak point of WebAssembly is first-load size and time; section 5.6 covers mitigation.

### Component library (decision needed; prototypes will compare)
| Option | License / cost | Fit |
|---|---|---|
| MudBlazor | Free, open source (license text to be confirmed). Supports .NET 8, 9 and 10. | Largest Blazor community, Material look, good data grid and mobile components. Recommended starting point. |
| Radzen Blazor | MIT core; premium themes are paid | Strong data grid and forms. Free themes are plainer. |
| Fluent UI Blazor | License not yet verified | Microsoft look; feels like an office app. Less suited to a fun pool site. |
| Keep Bootstrap, new theme | Free | Lowest risk, least improvement. Not recommended. |

Rules for whichever we pick: no paid licenses, pin exact versions, no CDN for the library itself (ship it with the app so one deploy is self-contained).

### Sign-in (decision needed)
- Today the Log In link opens a chooser page (`Login.razor`) with Microsoft and Google. Personal Microsoft accounts worked last season (owner, 2026-10-28). Three paths skip the chooser and go straight to Google: loading `/login` directly or refreshing it (`staticwebapp.config.json` rewrites `/login`), the "please sign in" redirect when an API call needs a login (the 401 override), and the not-authorized link in `App.razor`. Microsoft's current docs list only GitHub and Microsoft Entra ID as preconfigured providers on all plans. Google still redirects today (checked 2026-10-28) but is undocumented. Custom providers require the Standard plan (about $9 per month).
- Options:
  1. Keep Google only. No cost. Risk: it could stop working with no notice.
  2. Offer Google and Microsoft side by side on a clear sign-in page that explains, in plain words, what will happen. No cost. Gives a fallback if Google stops.
  3. Standard plan with an email-link or custom provider. Costs money. Defer.
- Recommendation: option 2 in Release 2, plus making sign-in unnecessary for everything except entering your own picks (section 5.1).
- **Decided 2026-10-28: option 2, which is what the site already does.** Google and Microsoft side by side; each player chooses. Release 2 sends the three Google-only paths above to the chooser page too, and rewrites the page in plain words.
- **A new login is a new user** (owner decision, 2026-10-28). No linking or merging of accounts. Sign-in stays with third-party providers only; the site never stores passwords.
- The hard part is explaining sign-in to older players. The chooser page uses the words people know: "Sign in with your Gmail account" and "Sign in with your Outlook, Hotmail or Live account", with a line saying the site never sees their password.
## 5. UX direction (to be validated with prototypes)

Principles: large text and tap targets, one clear action per screen, plain labels (keep "Leaderboard"; "Standings" may be used interchangeably), no sideways scrolling on phones, every important page prints well.

1. **No sign-in needed to follow along.** Standings, scoreboard, Path to Victory and printing stay public. Add "Find my entry": a search or tap on your name that highlights your row and remembers it on that device (browser storage only, no account).
2. **Standings first.** Home page is the standings for the remembered pool, with the player's own entries pinned at top. On phones, show Rank, Name, Points, Max; tap a row for detail. Clear status line: "Picks lock Sat Dec 12, 11:00 AM CT" or "Updated 8:42 PM CT".
3. **Printing is a feature.** Print buttons on standings, a single entry, the all-picks grid, and the blank entry form. Print layouts designed for one landscape page each.
4. **Blank entry form and fast paper entry match.** The printed blank entry form fits on one landscape page, always (owner requirement). Playoff structure on the form: every playoff game except the championship is a named bowl; first-round games have both teams set; each quarterfinal has its top seed set and a blank for a first-round winner; semifinals and the championship are all blanks. The tiebreaker line names the pool's tiebreaker game. It and the admin entry screen list games in the same order. Admin entry is keyboard-driven (one key per game for the first or second team listed), with a running count and a check before save. No position numbers are printed next to teams; numbers shown with a team are AP rankings only.
8. **Team logos throughout.** Logos were popular last year. Use the existing `TeamLogo` component (logo image, or team-colored abbreviation when missing) on pick buttons, scores, admin entry and print. Not on the leaderboard: one pick shouldn't label a whole entry (owner decision, 2026-10-28).
10. **Scores.** Games grouped as Today, Coming up and Earlier results. Each game shows logos, AP rank, playoff seed, score, live status from the feed's game detail, points at stake, the remembered entry's pick (with right or wrong once final), and how the pool split its picks (after lock only).
9. **AP rank as its own field.** Last year ranks were typed into team names. Proposed data model change: nullable home and away AP rank on `BowlGame`, shown as "#15 Utah". Needs owner approval and a business-rules note on where ranks come from (typed by admin, or from the data feed if it provides them; not yet checked).
5. **Path to Victory simplified.** Default view: "Who can still win, and what has to happen". The full grid stays for the guessing crowd.
6. **First load.** Show a styled loading screen inside `index.html` before Blazor starts, and show cached last-known standings while the API wakes (#117). The wake-up wait is a free-tier limit; this hides it, it doesn't remove it.
7. **Admin.** A season checklist page (create season, load games, link feed, set pool games, open pool, print blanks, key paper entries, mark payments). Admin dashboard shows only the current site's sport. Replace raw ids (season GUID in the entries list) with names.

## 6. Backlog placement

| Issue | Placement | Why |
|---|---|---|
| #93, #97, #98, #99 | Release 1 | Server-only, low UI risk, avoid mid-season fixes |
| #102, #107 | Release 1 | Needed for this season |
| #109, #94, #127, new rank issue | Release 1 | Bracket simulator first slice (section 12) |
| #125 printed tiebreaker, #126 year from clock | Release 2, with the print pages | Print pages are rebuilt then |
| #128-#137 | Release 1.1 | 2026 code review; see section 3 |
| #138 admin and client cleanup | Release 2 | 2026 code review |
| #139 hoops findings | Release 3 | 2026 code review |
| #140 data safety and query cleanup | After season | 2026 code review |
| #120 InitializeDatabase route | Release 1 if trivial, else after season | Only used for setup |
| #117 cold starts | Release 2 (mitigation only) | Section 5.6 |
| #101 football archive lookup | Before archiving (Jan 2027) | Not needed until then |
| #106 pool deletion design | After season | Needs a design; matters for friends' pools |
| #105, #103, #104 | Release 3 | Hoops |
| #108 cleanups | Any release, low priority | |
| #34 Rules page | Release 2 | Helps new and older players; small |
| #55 offseason score API | After season | |
| #54 Apple, #96 Email | Deferred | Owner decision |

## 7. Findings from the review

Sections 7.1 and 7.2 were filed as #136 and #137 on 2026-10-28.

### 7.1 Draft issue: Path to Victory ranks don't match the leaderboard
**What is wrong.** The football Path to Victory page ranks by score only, so entries tied on score share a rank. The leaderboard ranks by score, max possible, the pool's tiebreakers, then name (business rules section 4). The same pool shows different ranks on the two pages.

**Evidence.** `BowlPoolManager.Core/Helpers/WhatIfScoringEngine.cs:104-113` assigns a shared rank when `Score` matches the previous row. `ScoringEngine.cs:120-166` applies the full order. On production (test data, Family Pool, 2026-10-28) the leaderboard shows Sally 3rd with the bronze trophy; Path to Victory shows her tied for 2nd. Adam and Steve are 6th and 5th on one page and tied 5th on the other.

**Done looks like.** Both pages use the same rank order and tie rule. A test covers two entries tied on score but not on tiebreaker. Check `HoopsWhatIfScoringEngine` for the same problem.

### 7.2 Draft issue: players can't save an incomplete entry
**What is wrong.** Business rules section 3 says picks may be saved incomplete before lock. The football My Picks page refuses to save until every game has a pick.

**Evidence.** `BowlPoolManager.Client/Pages/MyPicks.razor:617-626` lists missing games and returns before saving when the pool is not locked. The server side was not checked.

**Done looks like.** A player can save part-way before lock and sees how many picks are left. Check `HoopsMyPicks.razor` and the server validators for the same rule.

### 7.3 Other observations
- First load of the leaderboard took over 10 seconds with only a spinner (cold API).
- Phone layout: double side padding, round columns hidden, Path to Victory grid scrolls sideways, Print button wraps.
- The admin dashboard on bowl-madness.com lists hoops admin tools too.
- Admin entries list shows the season GUID instead of the season name.
- The admin entry form's owner picker is a long dropdown of email addresses.
- Admin paper entry is one tall list of mouse-click cards in date order, about 26 games per entry last year.
- The printed bracket's tiebreaker line is hard-coded to "ReliaQuest Bowl (Iowa vs Vanderbilt)" (`PrintBracket.razor:169`). The pool's chosen tiebreaker game (`BowlPool.TieBreakerGameId`, business rules section 4) is ignored. The header year uses `DateTime.Now.Year` (line 53), so a form printed in January shows the wrong season. No open issue covers either.

## 8. Prototypes (next step)
Static HTML pages with sample data, published as private links. Proposed set:
1. Standings on phone and desktop, signed out, with "Find my entry".
2. Entry editor on phone (player) and the fast paper-entry screen (admin).
3. Print layouts: standings and a blank entry form.
4. The same standings page in two component-library looks, to choose between them.

## 9. Risks
- Schedule: Release 2 has about four weeks. Fallback is Release 1.
- Google sign-in is undocumented on our plan.
- A new component library changes every page; regressions in print layouts are likely. Print must be tested by hand.
- Today the PR preview uses the production database, so write paths can't be tested there. Phase 0's test site (section 13) removes this risk; until it exists, test writes only with the test suite.

## 10. Decisions needed from the owner
1. ~~Approve the three-release plan and dates.~~ Approved 2026-10-28.
2. ~~Component library.~~ MudBlazor, decided 2026-10-28. Bottom navigation on phones approved.
3. ~~Sign-in option.~~ Google and Microsoft, player's choice, decided 2026-10-28. A new login is a new user.
4. UX direction in section 5 (after prototypes). Labels: keep "Leaderboard" (decided 2026-10-28). Prototypes looked good to the owner on 2026-10-28; the detailed Release 2 plan settles the rest.
5. ~~File the issues in 7.1 and 7.2.~~ Filed as #136 and #137.
6. ~~Path to Victory approach.~~ Shared simulator in Core, running in the browser (section 12), decided 2026-10-28.
7. ~~Add a field for which line a winner fills.~~ Not needed; one shared start time rule (section 12), decided 2026-10-28.
8. ~~Replay last season's archive as a test.~~ Approved 2026-10-28: read-only export, names replaced.
9. ~~Ordering.~~ Start time orders everything, decided 2026-10-28.
10. ~~Release 1 split.~~ 1.0 is the .NET 10 upgrade only; 1.1 is one release of four groups, decided 2026-10-28.
11. ~~Review placements.~~ Findings #128-#140 placed as in section 6, approved 2026-10-28.
12. ~~Move Entry.~~ Rebuild it (#132); admins must be able to move an entry to the right pool. Decided 2026-10-28.
13. ~~Invite code across seasons.~~ Use the current season's pool (#135), decided 2026-10-28.
14. ~~Test environment.~~ Sister static web app plus its own serverless Cosmos account (section 13), decided 2026-10-28 and 2026-10-29.
15. ~~Score feed.~~ Keep College Football Data for football this season, behind a shared feed interface with a mock (section 13), decided 2026-10-28.
16. ~~Phase 0 decisions.~~ A `staging` branch for releases; known failing tests labelled `KnownFailure` and skipped in CI; test-site page views accepted in production telemetry. Decided 2026-11-22 (docs/plans/phase-0-test-environment.md).

## 11. Prototypes
Canvas: https://claude.ai/artifact/F2wyGerBb6cgyCayDV82Z3 (private). Sample data only.

## 12. Path to Victory: shared bracket simulator
Decided 2026-10-28: one simulator in Core, running in the browser. The server gains no what-if endpoint. Its only Path to Victory change is #97, sending picks only after lock.

### Why
- Games store `NextGameId` but not which slot (home or away) the winner fills. The server also copies real winners into the next game's team names. So every page works out the bracket again: `GameScoringService.PropagateWinner`, `Admin/GameResults.razor`, `ScoreboardCard.razor`, `PrintBracket.razor` and `WhatIf.razor`, plus the hoops copies. They use three different rules.
- About 270 lines of simulation logic sit in `WhatIf.razor` with no tests. They rely on each playoff game starting after the games that feed it, but nothing checks that.
- Path to Victory ranks differ from the leaderboard (7.1), elimination is missing (#109), and simulated Max is too high (#127).

### Design
- One class in Core. Input: games, entries, pool settings, and the winners the viewer has chosen. Output:
  - for each game, its two teams, its winner, and whether that winner is real or chosen;
  - for each entry, score, Max, rank and whether it's out of the top 3;
  - for each pick, won, lost, dead (the team is already out) or open.
- Start time sets the order everywhere on the site: pick sheets, the paper form, admin entry, Path to Victory, and the order the simulator works through games (owner decision, 2026-10-28). Admins enter bowls in any order, so the order they were entered never matters. The simulator checks that every playoff game starts after the games that feed it and reports any that don't. That catches a mistyped kickoff time instead of quietly giving wrong results.
- Ranking uses the leaderboard's code, so the two pages can't disagree.
- A chosen winner that is no longer playing in that game is ignored. The page no longer clears downstream choices itself.
- The page only draws the output. Clicking a team records one choice and asks the simulator again. The page holds no bracket or scoring logic.
- The server's winner propagation and the scoreboard, print and admin pages use the same slot function.
- No new field for which line a winner fills (owner decision, 2026-10-28). One shared, tested function in Core applies the start time rule: if one line already has a team, the winner fills the empty line; otherwise the earlier kickoff goes to the top (home) line. Every page and the server's winner propagation use this function. Revisit only if a friend's pool needs a custom bracket layout.

### Testing
The logic moves out of the page, so it can be tested with the existing xUnit, FluentAssertions and Moq. No page-testing library is needed.
1. **Scenario tests.** A small builder makes a 12-team bracket plus bowl games. Tests cover each round, upsets, choices that become invalid when an earlier choice changes, games ending without a winner, missing picks, entries with no picks, and #127's example.
2. **Rules that must always hold**, checked against a few thousand generated seasons (seeded random numbers, so failures can be repeated):
   - Score is never more than Max.
   - With no chosen winners, the simulator's standings and ranks match `ScoringEngine` exactly.
   - When every game is decided, Max equals Score.
   - A team that has lost never adds to anyone's Max.
   - Every winner is one of the two teams in that game.
   - Choosing the real result changes nothing.
3. **Replay of last season (approved 2026-10-28).** During Release 1, export last year's archived pools once with GET requests only, replace player names, and save the result as a test file. The test replays results in start time order and checks the final points, correct picks and ranks against the archive.
   - Football archives keep games, scores, point values and each entry's picks. They don't keep the bracket links, playoff rounds, Max or the pool's tiebreaker settings. Those come from last season's game records, if they still exist, and the pool record, also read-only. If not, the replay checks scoring and ranking only.
   - The archived standings came from the old code, so they are a baseline to match, not proof of correctness. Any difference gets checked by hand against what was actually paid out.
4. **Same answer as the server.** Tests check that the server's winner propagation and the simulator put each winner in the same slot.
5. **By hand.** Before Release 1.1 and Release 2, click through a checklist on the test site (section 13): reset, change an earlier result, pick an upset, and compare ranks with the leaderboard.

## 13. Test environment and score feed mock
**Decided 2026-10-28:** a sister test site (a second Free static web app) with its own Cosmos DB account, because preview environments inherit production settings. Mock the score feeds. Score feed provider decided the same day (see below).

Owner request, 2026-10-28: test each release fully before production without touching live data, and test scoring before this season's bowl games exist in the score feed.

### Facts checked
- Static Web Apps copies application settings into every preview (staging) environment, and settings can then be changed per environment (Microsoft Learn, "Configure application settings", updated 2026-01-23). So each new PR preview starts with the production connection string. That is why previews use the production database today, and changing a preview's settings after it is created leaves a window where it doesn't.
- Free plan: 3 staging environments per app; managed functions only (Microsoft Learn, "hosting plans").
- The database name is a constant (`Core/Constants.cs:7`, `BowlMadnessDb`); the account comes from the `CosmosDbConnectionString` setting (`Api/Program.cs:34`).
- Football scores come from College Football Data (`CfbdService.GetScoreboardGamesAsync`, behind `ICfbdService`, registered at `Api/Program.cs:23`), not ESPN. ESPN supplies hoops scores and logos.
- Checked 2026-10-29: the subscription has 2 of its 10 Free static web apps, and no Cosmos DB account uses the free tier (one allowed per subscription). The free tier works only with provisioned throughput, not serverless: 1000 RU/s and 25 GB free, chosen at account creation. 
- **Decided 2026-10-29: the test account is serverless (pay per use), like production.** Production's Cosmos account cost under $1 over the last 12 months, including the whole bowl season (owner, from Cost analysis). Serverless leaves the subscription's one free-tier slot unused and has no charge for reserved capacity if settings change.

### Design (decided)
- **A second Free static web app for testing**, with its own settings, deployed from pull requests and a `staging` branch. Production's PR previews stop being used for testing.
- **A separate Cosmos DB account for testing**, serverless, in its own resource group (`rg-bowlmadness-test`). A separate account means a test setting can never reach production data, even by mistake.
- **Guards in code.** A `SiteEnvironment=Test` setting turns on test-only features (reset, seed, mock feed). Those features also refuse to run if the database account is the production one. Production never sets it.
- **Seed and reset** from an admin page button (server-checked as above), with a few named starting points: before Selection Day, entry window open, mid-season, and last season replayed (from the approved archive export, names replaced). Seed data lives in the repository as test fixtures; nothing is copied from production.
- **Score feed mock.** An implementation of the shared score-feed interface (below), used when `ScoreFeed=Mock` (test only). It reads games and scores from the test database. An admin "score feed" page lists games and lets an admin set each one to scheduled, in progress (quarter and clock) or final with scores, and change the team names the feed reports, to test mismatches (#133). It also serves the postseason game list, so loading bowls (#102) and linking can be tested before Dec 6. It starts from a one-time recording of last season's real postseason data. A hoops (ESPN) mock waits for Release 3.
- **Timing.** Test environment before 1.0, with the test baseline, so 1.0 is tested there. Score mock at the start of 1.1.

### Score feed provider (decided 2026-10-28)
- Today: football uses College Football Data for three things: the postseason game list (import and linking; `ExternalId` holds its game id and `ApiHomeTeam`/`ApiAwayTeam` its team names), live scores (`GameScoringService`), and FBS team data (`SyncTeams`). Hoops uses ESPN for scores and logos and College Basketball Data for teams. The owner chose College Football Data because a documented, keyed service is less likely to change without notice than ESPN's unofficial endpoints; no equal option existed for hoops.
- Decided: keep College Football Data for football this season. Put both sports' score feeds behind one internal score-feed interface (one shape for "games and scores"), with the College Football Data and ESPN code as two implementations and one mock for testing. Football adopts it in 1.1, hoops in Release 3. Switching football to ESPN, or using ESPN as a fallback when College Football Data is down, then becomes a contained change to revisit after the season. Owner approved the new pattern.
