# Phase 2 (Release 2): new UI, football player pages and season admin

Status: approved 2026-11-23. Part of docs/UPGRADE-PLAN-2026.md (sections 3, 4, 5, 8 and 11). One release, built as five groups; run each group in its own session.

## Start here (for a new session)
1. Read CLAUDE.md, docs/BUSINESS-RULES.md, docs/UPGRADE-PLAN-2026.md sections 3, 4, 5 and 11, then this file.
2. Confirm Phase 1.1 is done (its "Release progress" line) and the revert point below exists. If not, stop and tell the owner.
3. Look at the prototypes (section 11 of the upgrade plan; the owner can share screenshots). They set the look and the flows. Their team names and bowls are sample data.
4. Find your group's "Progress" list below and continue from the first open item.

## Goal
Players, many of them older and half on phones, can follow the pool without signing in, find themselves, make picks and print forms easily. Admins can set up the season and key in paper entries fast. Everything runs on the new look (MudBlazor) and is in production by **2026-12-05**, the day before Selection Day.

## Dates
| Date | What |
|---|---|
| 2026-11-24 | Group A (foundation) starts |
| after group A | Look checkpoint: the owner reviews the reference Leaderboard and the component sheet on the test site and signs off the look. Groups B, C and D start only after that. |
| 2026-12-03 | Go/no-go: groups A to D merged into `staging` and the release checklist passed on the test site, on a phone and in print preview |
| 2026-12-05 | Freeze: last day a Release 2 change may reach production (owner decision, 2026-11-23) |
| 2026-12-06 | Selection Day: admin loads games, opens pools, prints blanks |

If the go/no-go fails, production stays on Phase 1.1 for the bowl season, and Release 2 ships after the championship.

## Revert point
- Before group A starts, tag the last Phase 1.1 code on `main` as `release-1.1` (`bd7c70c`, the merge of PR 167). The owner approves the tag push.
- To go back after a Release 2 release: a new PR that reverts the release merge commit (`git revert -m 1 <merge>`), through `staging` to `main` like any release. Never force-push or reset.
- Data must stay readable by Phase 1.1 code. Release 2 may only add fields (nullable, with defaults), never rename or remove one. A Phase 1.1 save after a revert drops fields it doesn't know (for example an AP rank), which is acceptable.
- What a revert loses: only the new look and features. Picks, entries, payments and results are untouched. "Find my entry" lives in each browser and is simply ignored.

## Decided (do not reopen)
- Blazor WebAssembly stays. MudBlazor is the component library (2026-10-28). Use 9.14.0 (latest stable, MIT, supports net10.0; checked on NuGet 2026-11-23). Pin the exact version; ship it with the app, no CDN.
- Bottom navigation on phones: Leaderboard, Scores, What If, My Picks (prototype 1).
- Sign-in: Google and Microsoft side by side, the player's choice; a new login is a new user. All three Google-only paths go to the chooser (`staticwebapp.config.json` `/login` rewrite and the 401 redirect, the not-authorized link in `App.razor`). Plain words: "Sign in with your Gmail account" and "Sign in with your Outlook, Hotmail or Live account", and a line saying the site never sees their password.
- Label "Leaderboard" stays. No team logos on the leaderboard.
- Printing: one landscape page each; the blank entry form fits one page always. Playoff layout on the blank form as in upgrade plan 5.4.
- Path to Victory runs on the shared simulator from Phase 1.1 group C; only its screen changes.
- Hoops pages keep working inside the new layout but are not redesigned (Release 3).

## Decisions made 2026-11-23
1. ~~Fonts.~~ Decided: (a), ship the font files with the app. The prototypes use Atkinson Hyperlegible (body, made for low vision) and Barlow Condensed (headings), both free (SIL Open Font License). Options: (a) ship the font files with the app; (b) load them from Google Fonts; (c) system fonts only. Recommendation: (a), the same rule as for the component library, one deploy self-contained, no third-party call.
2. ~~AP rank.~~ Decided: (a). Options were: (a) add nullable home and away AP rank to `BowlGame`, typed by the admin on the games page, shown as "#15 Utah" (a data model change); (b) keep typing ranks into team names as last year; (c) no ranks this season. Recommendation: (a), typed by hand; the feed's rank data is not checked. Needs a BUSINESS-RULES line on where ranks come from.
3. ~~Groups in parallel.~~ Decided 2026-11-23: group A ends with a look checkpoint (a reference Leaderboard and a component sheet the owner reviews and corrects). After the owner signs off the look, groups B, C and D run in parallel sessions, each in its own worktree, built only from group A's shared components. A later correction to the look changes a shared component, not each page.
4. ~~Component tests.~~ Decided: (a). Options were: (a) no new test library: keep logic out of pages, in Core classes with xUnit tests, and check pages by hand; (b) add bUnit (free, MIT) for page tests. Recommendation: (a) this season, because the risk in this release is how pages look, which bUnit doesn't check.

## Rules for every group
- Pages keep calling the same API endpoints. Server changes only where a group says so, and every write endpoint keeps its caller check first.
- Logic that isn't drawing (sequence parsing for fast entry, find-my-entry matching, ordering games for print and entry) lives in Core with xUnit tests. Tests first for new logic.
- Every page works at phone width (390 px) with no sideways scrolling, text at least 16 px (names, numbers and body text; labels such as tab names, column headings, notes and badges may be 14 to 15 px, as in the prototypes, owner decision 2026-11-23), tap targets at least 44 px, and good contrast. Check each changed page at phone and desktop width in the browser pane, and print pages in print preview.
- Times through the Central-time helper, always with "CT" (#107, #168).
- Run code-review, and security-data-review when a change touches sign-in, writes, picks visibility or permissions.
- Verify on the PR's test-site preview before asking to merge. Never write to production.
- Show the owner screenshots of each changed page (phone and desktop) in the summary. UI choices not settled by the prototypes or this plan go to the owner with options first.
- `Closes #N` or `Refs #N` for each issue.

---

## Group A: foundation and the look
**Why first:** every page sits inside the layout and theme, and the owner settles the look here, once, before pages are built in parallel.

**Work:**
1. MudBlazor 9.14.0 installed and pinned; providers in `MainLayout`; Bootstrap kept only where a page still needs it until its group replaces it.
2. Theme from the prototypes: navy `#0F2747`, orange `#C2410C`, amber `#F59E0B`, page `#F4F5F7`; fonts per open decision 1.
3. New `MainLayout`: top bar with the site name and sign-in, pool switcher, bottom navigation on phones, side or top navigation on desktop, admin links for admins. `PrintLayout` unchanged.
4. Hoops: the hoops pages and navigation still work on hoops-madness.com inside the new layout (no redesign).
5. Loading screen in `index.html` before Blazor starts (#117 mitigation).
6. Sign-in: the chooser page in plain words; the three Google-only paths go to it.
7. Every existing page still loads and works in the new layout (a quick pass; deep restyling belongs to later groups).
8. **Shared components** in the Client (one folder, used by every later group): page header, status line, card, table row, team pick button (with `TeamLogo` and AP rank or seed), primary and secondary buttons, notice, empty state, bottom navigation. Later groups use these and do not restyle pages with their own one-off styles.
9. **Reference page:** the Leaderboard finished on phone and desktop (prototypes 1 and 3): status line, rows, "out" marker from the simulator, print button. Find my entry is group B.
10. **Component sheet:** an Admin-only page that shows every shared component in its states (normal, selected, disabled, error), at phone and desktop width.
11. **Look checkpoint:** after the PR's checks pass, the owner reviews the reference page and the sheet on the PR preview (on a real phone too) and asks for corrections; the session makes them in the shared components; repeat until the owner signs off. Then merge.

**Decided in group A (2026-11-23, owner):** phones: History, Print Bracket, Admin and Log out are in a menu button in the header (the four tabs stay in the bottom bar). The pool switcher is header tabs on phones and a segmented control on wide screens, shown only on pages that use pools (`PoolSelection`). Desktop admins get one "Admin" item that opens the Dashboard, which has a Component Sheet tile. Hoops uses the same four tabs (Scoreboard is "Scores", Path to Victory is "What If" on both sites). Shared components are documented in docs/UI-COMPONENTS.md.

**Progress:** [x] revert tag [x] MudBlazor + theme [x] layout + navigation [x] hoops check [x] loading screen [x] sign-in [x] all pages load [x] shared components [x] reference Leaderboard [x] component sheet [x] reviews [ ] test site check [ ] look signed off by the owner [ ] merged

---

## Group B: following along (no sign-in)
**Pages:** Leaderboard (home), Scores, Entry details, History and Archives.

**Work:**
1. Leaderboard as the home page (the page itself is group A's reference page).
2. **Find my entry:** tap your name, confirm, and this device remembers it (browser storage only, per pool). The remembered entry is pinned at the top with place and Max. "Not you? Forget me".
3. Scores, prototypes 7a and 7b: Today, Coming up, Earlier results; logos, AP rank, seed, score, live status, points at stake; the remembered entry's pick (right or wrong once final); the pool's pick split after lock only.
4. AP rank per open decision 2, if (a): the field, the games-page input and display.
5. Entry details and History restyled.

**Progress:** [ ] leaderboard as home [ ] find my entry [ ] scores [ ] AP rank [ ] entry details + history [ ] reviews [ ] test site check [ ] merged

---

## Group C: picks and paper entry
**Pages:** My Picks and the entry editor, admin entries list and entry form, payments.

**Work:**
1. Entry editor, prototype 2: big team buttons with logos, progress bar, lock time, "save part-way" note. Keeps every Phase 1.1 rule: unresolved slots disabled, dependent picks cleared with a notice, incomplete saves allowed.
2. **Fast paper entry**, prototype 4: games in the same order as the printed blank form; one keystroke per game (1 = first team listed, 2 = second); click a team to fix one; running count; a check before save; "Save and start next entry". Owner optional ("Leave blank for players who never sign in"). The server rules from Phase 1.1 apply unchanged.
3. Admin entries list: names instead of season GUIDs; Move Entry kept.
4. Payments restyled.

**Progress:** [ ] entry editor [ ] fast entry order shared with print (Core) [ ] fast paper entry [ ] entries list [ ] payments [ ] reviews [ ] test site check [ ] merged

---

## Group D: printing
**Pages:** print bracket (blank form and single entry), print standings, all-picks grid.

**Print checkpoint:** printing has its own look (black and white, a fixed page). Build a first draft of the blank entry form first and show it to the owner in print preview (a screenshot of the preview, plus the preview address) before building the other print pages.

**Work:**
1. Blank entry form, prototype 5b: one landscape letter page always, bowls in three columns, the playoff tree as in upgrade plan 5.4, the pool's tiebreaker game named (#125), the year from the season (#126). Same game order as fast paper entry (group C's Core helper).
2. Single entry print and the all-picks grid.
3. Standings print, prototype 5a.
4. Checked in print preview in Chrome and Edge at letter landscape, with a full season's game count (last season's pools had 34 games).

**Progress:** [ ] blank form draft shown to the owner [ ] blank form [ ] #125 [ ] #126 [ ] single entry + grid [ ] standings [ ] print preview checks [ ] reviews [ ] test site check [ ] merged

---

## Group E: Path to Victory, season admin, rules (ships if ready by the go/no-go; otherwise after the season)
**Work:**
1. Path to Victory: default view "Who can still win, and what has to happen"; the full grid stays one tap away. Draws only the simulator's output.
2. Admin season checklist page (create season, load games, link feed, set pool games, open pool, print blanks, key paper entries, mark payments), each step linking to its page.
3. Admin dashboard shows only the current site's sport.
4. Rules page (#34).
5. #138 items that fall on pages this release rebuilt.

**Progress:** [ ] path to victory view [ ] season checklist [ ] dashboard [ ] rules page [ ] #138 [ ] reviews [ ] test site check [ ] merged

---

## Release
1. Go/no-go on 2026-12-03 (above).
2. Release checklist on the test site with `entry-window` and `mid-season`: every player page signed out and signed in, on a phone width and on desktop; a full paper entry keyed in; every print page in print preview; admin season steps; hoops pages load.
3. The owner clicks through on a real phone.
4. Release PR from `staging` to `main` (merge commit). After release: read-only production check and log check.
5. Update docs/UPGRADE-PLAN-2026.md, ARCHITECTURAL_CONTEXT.md (component library, layout, find-my-entry storage), and propose BUSINESS-RULES edits (find my entry, AP rank source).

**Release progress:** [ ] go/no-go [ ] checklist [ ] owner phone check [ ] released [ ] production check [ ] docs
