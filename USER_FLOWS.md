## User Roles and Relationships

Roles are cumulative — higher roles inherit all capabilities of lower roles.

Visitor > Player > Admin > SuperAdmin

- **Visitor** Unauthenticated user role.
- **Player** Base authenticated role. Can do everything a visitor can, plus player-specific tasks.
- **Admin** Can do everything a player can, plus admin-specific tasks.
- **SuperAdmin** Can do everything a player and admin can, plus superadmin-specific tasks.

When reasoning about impact of changes, remember:
- Anything that breaks a Player flow breaks ALL roles.
- Anything that breaks an Admin flow also breaks SuperAdmins.
- Test flows with multiple roles. Do not just test player-centric flows with the player role.

---

## Pool State Machine

Pools move through states that control visibility and editability across the entire site:

```
ACTIVE → LOCKED → CONCLUDED → ARCHIVED
```

- **ACTIVE**: Lock date has not passed. Players can join and edit picks. Picks are hidden from the leaderboard and entry detail views.
- **LOCKED**: Lock date has passed. Picks are now visible to all users on the leaderboard and entry detail pages. Players can no longer edit picks (name-only edits still allowed). Games are being played and results entered.
- **CONCLUDED**: SuperAdmin has marked the pool finished. Trophy icons appear on the leaderboard. Pool is still visible in the main season view.
- **ARCHIVED**: Pool has been moved to history. No longer appears in the main leaderboard; only accessible via the Pool History page.

SuperAdmins can view picks at any state regardless of lock date.

---

## Flows

### Log In
**Who uses this:** Visitors

1. User arrives at the site unauthenticated.
2. User navigates to the Login page.
3. User authenticates via Microsoft or Google.
4. User is redirected to the home page.

---

### Log Out
**Who uses this:** All authenticated users

1. User clicks the Log Out link in the nav.
2. App clears the session.
3. User is redirected to the home page as a Visitor.

---

### Join a Pool and Submit Picks
**Who uses this:** Players

This is the primary player flow. Requires an invite code obtained outside the application (e.g., shared by a pool organizer).

1. Player navigates to **My Picks**.
2. Player clicks **New Entry** and enters the pool's invite code.
3. Player enters their display name.
4. Player selects a winner for each game in the pool.
5. Player enters a tiebreaker points total for the designated tiebreaker game.
6. Player saves the bracket.
7. Player may return and edit picks at any time before the pool lock date.
8. Once the lock date passes, the bracket becomes read-only (name-only edits are still permitted).

**Cross-cutting notes:**
- A player can create multiple entries in the same pool. The **New Entry** button remains available until the pool locks.
- Players can delete their own entries until the pool locks.
- Picks are not visible to other users until the lock date passes.
- Every save is recorded in the entry's audit log.

---

### Check Leaderboard
**Who uses this:** All users

1. User visits the home page (`/`).
2. If multiple pools exist for the current season, user selects a pool from the dropdown.
3. The application remembers the selected pool for other pages and subsequent visits.
4. User sorts by rank, player name, total score, max possible, correct picks, tiebreaker, or individual round scores.
5. User clicks a player name to navigate to that player's entry detail.

**Cross-cutting notes:**
- While the pool is ACTIVE, picks are hidden and max-possible is suppressed for non-SuperAdmins.
- Trophy icons appear for the top 3 finishers once the pool is CONCLUDED.
- Pool selection persists across Leaderboard, Scoreboard, and Path to Victory.

---

### View an Entry
**Who uses this:** All users (after lock date); SuperAdmins (any time)

1. User clicks a player name on the Leaderboard, or navigates directly to `/entry/{EntryId}`.
2. Entry detail page shows the player's picks for every game in the pool, with correct/incorrect indicators for completed games.

**Cross-cutting notes:**
- If the pool is still ACTIVE, picks are hidden from non-SuperAdmins.
- SuperAdmins see an admin edit bypass link on this page.

---

### Check Scoreboard
**Who uses this:** All users

1. User navigates to **Scoreboard**.
2. User views game cards showing bowl name, matchup, scores, status (Scheduled / In Progress / Final), start time, location, and TV network.

---

### Path to Victory (What-If Simulation)
**Who uses this:** All users (after lock date)

1. User navigates to **Path to Victory**.
2. User selects a pool (only pools past their lock date are shown).
3. The page displays a matrix: players across columns, games down rows, showing each player's pick per game and their projected score.
4. User clicks a game row to simulate a winner for that game; projected scores update in real time.
5. Simulating a playoff game clears any downstream simulated picks automatically (since the teams in later rounds depend on earlier results).
6. User clicks **Reset to Reality** to clear all simulations and return to actual results only.

---

### Print a Bracket
**Who uses this:** All users

1. User navigates to **Print Bracket**, or clicks a print button from an entry or the leaderboard.
2. User selects a pool (for a blank bracket) or arrives with a specific entry pre-loaded.
3. User triggers the browser print dialog.
4. The bracket renders in a print-optimised landscape layout with picks highlighted and correct/incorrect results colour-coded.

---

### View Pool History
**Who uses this:** All users

1. User navigates to **History**.
2. User sees a list of all ARCHIVED pools.
3. User clicks **View Archive** on a pool.
4. Archive viewer shows the final standings on the left.
5. User clicks a player row to view that player's picks with correct/incorrect indicators on the right.

---

### Edit an Entry (Admin correction)
**Who uses this:** Admins and SuperAdmins

Outside of the application, a player notifies an admin about an error in their picks, and the admin agrees to correct the error.

1. Admin navigates to **Admin Dashboard → Manage Entries**.
2. Admin filters by season and pool, locates the player's entry, and clicks Edit.
3. Admin makes corrections to the picks.
4. Application saves the entry and appends a record to the pick's audit log.

---

### Track Payments
**Who uses this:** Admins and SuperAdmins

1. Admin navigates to **Admin Dashboard → Payments**.
2. Admin selects the pool from the dropdown.
3. Admin optionally toggles "Show Unpaid Only" to focus on outstanding entries.
4. Admin checks off entries as paid using the toggle on each row.

---

### Manage Users
**Who uses this:** SuperAdmins

1. SuperAdmin navigates to **Admin Dashboard → Users**.
2. SuperAdmin views all registered users with their current role and account status.
3. SuperAdmin changes a user's role (Player / Admin / SuperAdmin) via the role dropdown.
4. SuperAdmin enables or disables a user account.

**Cross-cutting notes:**
- Role changes take effect on the user's next page load.
- Disabling an account blocks all authenticated actions for that user.

---

### Season Setup
**Who uses this:** SuperAdmins — run once at the start of each bowl season, in order

1. **Create a Season** (`Admin → Seasons`): Add a new season (by year) and mark it as the current season.
2. **Create Games** (`Admin → Games`): Add each bowl game with bowl name, matchup, date/time, point value, location, and TV network. For playoff games, configure the round and bracket linkage (which earlier games feed into this one).
3. **Create a Pool** (`Admin → Pool Settings`): Create a pool, assign it to the season, select which games are included, set the lock date, set the invite code, designate the tiebreaker game, and configure tiebreaker logic (Correct Picks vs. Score Delta, primary and secondary).
4. Distribute the invite code to players outside the app.
5. *(Optional)* **Link Games to API** (`Admin → Link Games`): Map each game to the external CFBD data feed so scores sync automatically rather than being entered manually.

---

### Enter Game Results
**Who uses this:** SuperAdmins — throughout the bowl season

1. SuperAdmin navigates to **Admin Dashboard → Results**.
2. SuperAdmin selects the season.
3. For each completed game, SuperAdmin sets the status to **In Progress** or **Final** and enters home and away scores.
4. Saving a result immediately updates the Leaderboard, Entry Details, and Path to Victory for all users.

**Cross-cutting notes:**
- For playoff games, the winning team propagates forward into downstream games automatically (filling the "TBD" slot in the next round).
- A diagnostic tool on this page verifies that bracket propagation is correct.

---

### Conclude and Archive a Pool
**Who uses this:** SuperAdmins — at the end of a season

1. All games are final and all results have been entered.
2. SuperAdmin navigates to **Admin Dashboard → Pool Settings**.
3. SuperAdmin clicks **Toggle Conclusion** — pool status moves to CONCLUDED. Trophy icons appear on the leaderboard for the top 3.
4. Admin uses the **Payments** page to confirm all entries are marked as paid.
5. When the pool is fully closed out, SuperAdmin clicks **Archive** on the pool.
6. Pool moves to ARCHIVED and disappears from the main season view; it remains accessible via **Pool History**.
