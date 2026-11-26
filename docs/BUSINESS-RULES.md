# Business Rules

The intended behavior of Bowl Madness and Hoops Madness. Where this document and the code
disagree, this document is the intent and the code is the bug.

## 1. Roles and permissions

*Source: `Api/Helpers/SecurityHelper.cs`, `Api/Functions/*Functions.cs`, `Client/wwwroot/staticwebapp.config.json`.*

- Roles are Player (the default), Admin and SuperAdmin. Each includes the one below it. The first SuperAdmin is the email named in the `BootstrapAdminEmail` setting.
- Only a SuperAdmin changes roles or disables accounts.
- A disabled user is treated exactly like an unauthenticated user, on every endpoint. *(Owner decision)* The one exception: a disabled user who asks for their own profile is told the account is disabled (403), so the site can say so before signing them out.
- Every rule is enforced by the API function itself. The site routing file protects only the admin pages, so hiding a button is never a protection.

| Tier | May do |
|---|---|
| Public | View the leaderboard, scoreboard, games, pools, seasons, archives and team lists |
| Owner (signed in) | Create, edit and delete their own entries (editing and deleting only before lock); view their own picks, and everyone's picks once the pool is locked |
| Admin | Everything an Owner can do for any entry at any time; set payment status; view the user list; enter results and link games. *(Owner decision: Admins can enter results and link games, and what an Admin can do is the same in football and hoops.)* |
| SuperAdmin | Everything an Admin can do, plus create, edit, delete, conclude and archive pools; create and edit games and brackets; manage users, seasons, integrations, team syncs, backups and migration |

## 2. Pools

*Source: `Api/Functions/PoolFunctions.cs`, `HoopsPoolFunctions.cs`, `ArchiveFunctions.cs`, `HoopsArchiveFunctions.cs`, `Core/Validation/*`.*

- A pool is Active until its lock date, then Locked, then optionally Concluded, then optionally Archived. Locked is derived from the date, not stored.
- A pool needs a name and an invite code. A new pool's lock date must be in the future.
- **An invite code identifies a pool.** *(Owner decision)* A player joins a pool by entering its code. The code is not a security measure: it is not secret, it is reused from year to year, and the server does not check it. When a code matches more than one pool, the current season's pool is used. Football joins only a current-season pool by code; hoops falls back to the first open match, because some hoops pools carry the year text instead of the season's ID (section 6).
- Concluding is a SuperAdmin toggle and the prerequisite for archiving. Once the pool is past its lock date, a concluded pool refuses all entry changes, including renames. It is reversible. It does not hide the pool and does not affect trophies. *(Owner decision: concluding exists only to enable archiving and to prevent unintended changes to a pool after it has ended.)*
- Archiving requires a concluded pool. It saves a snapshot of games, final standings and picks, and removes the pool from the main views. It cannot be undone: the snapshot drops entry IDs, owners, payment status and audit history. Football archives do not store playoff rounds; hoops archives store rounds and seeds.
- Deleting a pool deletes its entries. *(Owner decision)* Football games are shared between pools, so what happens to games when a pool is deleted or archived is undecided and needs a design.

## 3. Entries and picks

*Source: `Api/Functions/EntryFunctions.cs`, `HoopsEntryFunctions.cs`, `Core/Validation/EntryUpdateValidator.cs`, `HoopsEntryUpdateValidator.cs`.*

- **Locking.** Up to and including the lock date, an entry may be created and changed freely. After it, no new entries and no changes to picks or the tiebreaker; renaming is still allowed unless the pool is concluded or archived.
- **Admins ignore every lock** and may edit, delete or reassign any entry at any time. A player's save never moves an entry to another pool; only an Admin moves entries.
- **What a save can change.** A save sets only the bracket name (1 to 60 characters, trimmed), the picks and the tiebreaker (0 to 999). Picks must be for games in the pool, except that a pick already stored on the entry is kept. The owner, season, payment status, creation time and audit log come from the stored entry or the server.
- **Visibility.** Before lock, no one except Admins sees another player's picks, by any route (page, API, backup). The leaderboard shows names and scores only. Once the pool is locked, everyone can see everyone's picks. A player always sees their own. *(Owner decision)* On the public pages (Scores, Entry details) an Admin sees another player's picks before lock only in the admin section; the API still returns them to Admins. *(Owner decision, 2026-11-25)*
- A bracket name is unique within a pool, ignoring case. A player's save is always recorded as theirs.
- A player can delete their own entry until lock.
- **Payment status** is changed only by an Admin or SuperAdmin. A player's save must never set it. *(Owner decision)*
- Picks may be saved incomplete before lock.
- **Find my entry.** *(Release 2)* A visitor can tap a name on the Leaderboard and confirm "Is this you?". The choice is kept in that browser only (one entry per pool) and the server records nothing about it; and is forgotten with "Not you? Forget me". It is not a sign-in and gives no rights: it shows no picks before lock. A signed-in player's own entries are pinned at the top without tapping.
- **Pool pick split.** The Scores page shows how many entries picked each team only after the pool locks, and only for games with both teams decided.
- **Football picks must be reachable.** A pick in a later round must be one of the two teams the entry's own earlier picks lead to, or a team already placed in that slot. The server refuses a changed pick that is not. A stored pick that is saved again unchanged is left alone, so an old entry can still be renamed. Hoops is not checked this way yet.
- **Moving an entry.** Only an Admin or SuperAdmin moves an entry to another pool. The target must be in the same season, not archived, and must not already have that bracket name. The move is written to the entry's change history. Payment status stays with the entry; picks for games not in the target pool are kept but do not score. A player's save never moves an entry.
- **Admin-entered flag.** IsAdminEntered is set when an Admin or SuperAdmin creates an entry for another user. It is kept for audit only, and an existing entry keeps its stored value.

## 4. Scoring and ranking

*Source: `Core/Helpers/ScoringEngine.cs`, `Api/Functions/LeaderboardFunctions.cs`, `HoopsLeaderboardFunctions.cs`, `Client/Pages/Leaderboard.razor`, `HoopsLeaderboard.razor`.*

The same rules apply to football and hoops.

- A correct pick on a final game earns that game's points. Football points are set per game; hoops points come from a per-round table on the pool.
- Games cannot end tied. *(Owner decision)* A game is Final only with both scores and a winner. A Final without them is refused from the score feed and from the results page, and scoring treats such a game as undecided.
- **Max possible** is the current score plus the points of every unplayed pick whose team has not yet lost.
- **Rank order:** score, then max possible, then the pool's first tiebreaker, then its second, then name. *(Owner decision)*
- Entries that tie on all of those share a rank (1, 2, 2, 4), listed by name. Prizes for a shared rank are split evenly by hand. *(Owner decision)*
- **Football tiebreakers** are chosen per pool: correct-pick count, or closeness of the entry's guess to the tiebreaker game's total points.
- **Hoops has a tiebreaker**, such as total points in the championship game, and the entry form, leaderboard and lock rule all use it. *(Owner decision)*
- **Path to Victory elimination.** The top three places pay out in both sports. An entry is eliminated when it can no longer finish in the top 3. *(Owner decision)*
- A game counts toward "correct picks" and the "final games" total only if it awards points. The First Four round's point value is configurable; when it is 0, a First Four win does not count. *(Owner decision)*
- **Trophies** (gold, silver, bronze for ranks 1 to 3) appear as soon as the math is finished, meaning every entry's max possible equals its score. The pool does not need to be concluded. *(Owner decision)*

## 5. Live scores and result propagation

*Source: `Api/Services/GameScoringService.cs`, `HoopsGameScoringService.cs`.*

- Scores refresh lazily when games are read; games not linked to the feed are updated by hand.
- When a game goes final, its winner fills the next game's slot and cascades; a game that is no longer final resets that slot.
- When a game's winner advances, it fills the slot of the next game as follows: with two feeding games, the earlier kickoff goes to home; with one, the winner fills the slot that does not already hold a seeded team.
- **Mock score feed.** A SuperAdmin can switch football scores between Live (College Football Data) and Mock (games edited on the Score Feed page). It works on the test site and on production. While Mock is on, the site shows and scores the mock's results for linked games, for every visitor, and real scores are not applied. The Score Feed page shows a warning while Mock is on; no other page does. There is no automatic block (for example after a pool locks). The default, when nothing is stored, is Live. The test-data reset sets it back to Live. Switching to Mock first shows which linked games of the current season it could change (and their names), and the admin must type MOCK to confirm; the server enforces the same confirmation.
- **Switching back to Live** asks whether to reset the current season's football games. A reset is only possible as part of switching from Mock to Live. "Switch and reset" lists the games first, with a warning that every result on them is cleared whatever its source (real and hand-entered results too). It then clears the scores of linked games that have a result, sets them to Scheduled, and returns the later-round teams that those results had advanced to "Winner of ...". Pools, entries and picks are not changed, so standings return to zero. "Switch only" leaves results as they are. Slots edited by hand, and later games whose result was entered by hand, are left alone and listed.
- **Before the first reset on production**, run the `/data-change` procedure (confirms continuous backup is on and records the restore point).

- **AP rank.** *(Owner decision, 2026-11-23)* Each football game has an optional AP rank for the home and away team, typed by a SuperAdmin on the games page (1 to 25, or empty; the server refuses other values). It is not read from the score feed. It is shown as "#15 Utah". Choosing a different team for a slot on the games page clears that slot's rank. *(Owner decision, 2026-11-25)*
- **Game order.** *(Owner decision, 2026-11-25)* The Scores page orders games by kickoff, playoff or not, in three groups: Today (the Central calendar day: games starting today, games being played, and earlier games not yet marked Final), Coming up (later days) and Earlier results (Final games that did not start today, newest first). Everywhere else games are listed non-playoff games by kickoff, then playoff games by kickoff. Football archives keep no playoff round, so archived results are by kickoff.

## 6. Seasons and IDs

*Source: `Core/Domain/Season.cs`, `Api/Repositories/SeasonRepository.cs`, `Client/Services/SeasonService.cs`.*

- One season is current for both sports. Per-sport seasons are an acceptable later change.
- **The "2026" quirk.** Some hoops pools and their entries carry the season as the year text "2026" instead of the season's ID. Hoops pools are therefore always requested without a season filter, and an entry is always requested with its own stored season, never the current one. The intended end state is a single ID format.

## 7. Time zones

*Source: `Core/Helpers/DateTimeHelper.cs`.*

- Times are stored in UTC and shown to everyone in Central time, labelled "CT". Dates typed by admins are read as Central. *(Owner decision)* Showing local time is a later enhancement.

## Undecided

- What happens to football games when a pool is deleted or archived. Games are shared between pools.
- Whether Admins may create and edit game definitions, or only enter results and link games.
