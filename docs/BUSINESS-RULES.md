# Business Rules

The intended behavior of Bowl Madness and Hoops Madness. Where this document and the code
disagree, this document is the intent and the code is the bug.

## 1. Roles and permissions

*Source: `Api/Helpers/SecurityHelper.cs`, `Api/Functions/*Functions.cs`, `Client/wwwroot/staticwebapp.config.json`.*

- Roles are Player (the default), Admin and SuperAdmin. Each includes the one below it. The first SuperAdmin is the email named in the `BootstrapAdminEmail` setting.
- Only a SuperAdmin changes roles or disables accounts.
- A disabled user is treated exactly like an unauthenticated user, on every endpoint. *(Owner decision)*
- Every rule is enforced by the API function itself. The site routing file protects only the admin pages, so hiding a button is never a protection.

| Tier | May do |
|---|---|
| Public | View the leaderboard, scoreboard, games, pools (without invite codes), seasons, archives and team lists |
| Owner (signed in) | Create, edit and delete their own entries (editing and deleting only before lock); view their own picks, and everyone's picks once the pool is locked |
| Admin | Everything an Owner can do for any entry at any time; set payment status; view the user list; see invite codes; enter results and link games. *(Owner decision: Admins can enter results and link games, and what an Admin can do is the same in football and hoops.)* |
| SuperAdmin | Everything an Admin can do, plus create, edit, delete, conclude and archive pools; create and edit games and brackets; manage users, seasons, integrations, team syncs, backups and migration |

## 2. Pools

*Source: `Api/Functions/PoolFunctions.cs`, `HoopsPoolFunctions.cs`, `ArchiveFunctions.cs`, `HoopsArchiveFunctions.cs`, `Core/Validation/*`.*

- A pool is Active until its lock date, then Locked, then optionally Concluded, then optionally Archived. Locked is derived from the date, not stored.
- A pool needs a name and an invite code. A new pool's lock date must be in the future.
- **Invite codes are secret and handed out offline.** *(Owner decision)* The server verifies the code whenever an entry is created in a pool. The pool list returns codes only to Admins.
- Concluding is a SuperAdmin toggle and the prerequisite for archiving. Once the pool is past its lock date, a concluded pool refuses all entry changes, including renames. It is reversible. It does not hide the pool and does not affect trophies. *(Owner decision: concluding exists only to enable archiving and to prevent unintended changes to a pool after it has ended.)*
- Archiving requires a concluded pool. It saves a snapshot of games, final standings and picks, and removes the pool from the main views. It cannot be undone: the snapshot drops entry IDs, owners, payment status and audit history. Football archives do not store playoff rounds; hoops archives store rounds and seeds.
- Deleting a pool deletes its entries. *(Owner decision)* Football games are shared between pools, so what happens to games when a pool is deleted or archived is undecided and needs a design.

## 3. Entries and picks

*Source: `Api/Functions/EntryFunctions.cs`, `HoopsEntryFunctions.cs`, `Core/Validation/EntryUpdateValidator.cs`, `HoopsEntryUpdateValidator.cs`.*

- **Locking.** Up to and including the lock date, an entry may be created and changed freely. After it, no new entries and no changes to picks or the tiebreaker; renaming is still allowed unless the pool is concluded or archived.
- **Admins ignore every lock** and may edit, delete or reassign any entry at any time.
- **Visibility.** Before lock, no one except Admins sees another player's picks, by any route (page, API, backup). The leaderboard shows names and scores only. Once the pool is locked, everyone can see everyone's picks. A player always sees their own. *(Owner decision)*
- A bracket name is unique within a pool, ignoring case. A player's save is always recorded as theirs.
- A player can delete their own entry until lock.
- **Payment status** is changed only by an Admin or SuperAdmin. A player's save must never set it. *(Owner decision)*
- Picks may be saved incomplete before lock.

## 4. Scoring and ranking

*Source: `Core/Helpers/ScoringEngine.cs`, `Api/Functions/LeaderboardFunctions.cs`, `HoopsLeaderboardFunctions.cs`, `Client/Pages/Leaderboard.razor`, `HoopsLeaderboard.razor`.*

The same rules apply to football and hoops.

- A correct pick on a final game earns that game's points. Football points are set per game; hoops points come from a per-round table on the pool.
- Games cannot end tied. *(Owner decision)*
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
