# Shared UI components (Release 2)

How to build a page in the new look. Every page uses these components and the classes in `wwwroot/css/ui.css`; no page adds its own one-off styles. If something is missing, ask for a new shared component (or a new state of one) instead of styling around it: a later correction to the look then changes one place.

The admin page `/admin/components` (Component sheet) shows each component below in every state, at phone and desktop width. The reference page is `Pages/Leaderboard.razor`: copy its pattern.

Where things live:

| What | Where |
|---|---|
| Components | `BowlPoolManager.Client/Ui/` (namespace `BowlPoolManager.Client.Ui`, imported everywhere) |
| Theme (colors, fonts for MudBlazor) | `Client/Theme/AppTheme.cs` |
| Colors, fonts, page frame (header, bottom bar) | `Client/wwwroot/css/theme.css` |
| Component styles | `Client/wwwroot/css/ui.css` |
| Fonts (shipped, no CDN) | `Client/wwwroot/fonts/` (OFL licences in `LICENSES.txt`) |
| Frame: `MainLayout`, `AppHeader` | `Client/Layout/` |
| Logic that is not drawing | `Core/Helpers`: `SiteNavigation`, `StatusLineText`, `TopThreeRule`, `PoolPicker` (xUnit tests in `Tests/Core`) |

## Rules every page follows
- Wrap the page in `PageContainer` and start with `PageHeader`. The browser tab title comes from `PageHeader`; do not add a `PageTitle`.
- Phone width is 390 px: no sideways scrolling, names, numbers and body text at least 16 px (tab names, column headings, notes and badges may be 14 to 15 px), tap targets at least 44 px.
- At 900 px and wider the layout is "wide" (top navigation); below it is "phone" (bottom navigation). Use `bm-only-phone` and `bm-only-wide` to show something at one size only.
- Times come from `DateTimeHelper.ToCentralLabel` (always "CT"). Status text comes from `StatusLineText` in Core.
- Colors, spacing and type sizes are not typed into pages. Use the components; the few helper classes are in ui.css (`bm-note`, `bm-section-title`, `bm-only-phone`, `bm-only-wide`, `bm-sr-only`).
- Icons are MudBlazor icons (`Icons.Material.Outlined.*`), not Bootstrap Icons. Bootstrap stays only on pages that have not been moved yet.
- Do not call MudBlazor components directly for something a shared component already does.

## Page width: one rule for the whole site
Every page is as wide as the header (1240 px) and centered, so moving between pages never jumps. This includes the old pages that still use `container-fluid` (Scores, My Picks, Entry details, History, the hoops pages): `MainLayout` caps the page area, so they need no change. Only big grids get the full screen: What If (football and hoops) and every page under `/admin`. In full-width mode the What If grids fill the width: the football matrix keeps its player-name column at 180 px and the game columns share the rest (never narrower than 70 px each, so a narrow screen scrolls sideways inside the box); on hoops What If the leaderboard keeps its width and the region bracket's round columns share the rest (never narrower than 220 px). The heading and controls share the grid's left and right edges.

- The rule lives in one place: `PageWidth.IsWide(path)` in Core (xUnit tests in `Tests/Core/PageWidthTests.cs`) decides, `MainLayout` adds the class `bm-article--wide`, and `theme.css` does the sizing. A page does not set its own page width.
- To make another page full width, add its address to `PageWidth.IsWide` and its test. Do not add `max-width` or `container-fluid` tricks to the page.
- `PageContainer` (`.bm-page`, 1240 px) and the layout cap agree, so new pages and old pages line up.
- Phone view and printing are not affected: the cap only applies above 1240 px, and printing removes it.

## The page frame (nothing to do in a page)
`MainLayout` draws the navy header (site name, the main links on wide screens, sign-in or "Hello, name / Log out", the phone menu), the bottom navigation on phones, and the page background. The links come from `SiteNavigation` (Core): one list per site, football and hoops. Hoops pages use the same frame; their content is not restyled yet. The last tab says "Enter Picks" instead of "My Picks" while the player has no entry (or is signed out); `MainLayout` checks `GetMyEntries` / `GetMyHoopsEntries` and re-checks when a picks page raises `AppState.OnChange`.

To show the header pool switcher on a page that has pools, inject `PoolSelection`, call `Show(pools, currentId)` after loading, handle `Changed`, and call `Clear()` in `Dispose` (see Leaderboard). Use `PoolPicker.Pick` (Core) to choose the first pool.

## Components

### PageContainer
Centers the page, at most 1240 px wide, with the standard padding. `Narrow="true"` makes it 640 px (forms, messages, sign-in).

### PageHeader
`Title` (the page `h1`), `Subtitle` (for example the pool name), `Status` (the status line text), `StatusTone` (`Normal`, `Live`, `Alert`), `StatusIcon`, `DocumentTitle` (tab title when different), and an `<Actions>` fragment for buttons at the right of the title.

### StatusLine
One line of small text with an optional icon. Used inside `PageHeader`; use it alone for a status inside a card. Draws nothing when `Text` is empty. Build the text with `StatusLineText.ForPool(...)` for pool pages.

### Card
`Tone`: `Default`, `Outlined`, `Highlight` (orange border: "this is you"), `Tint` (soft orange hint), `Dark` (navy, for a question). Optional `Eyebrow` (small orange label), `Title`, `Flush` (no padding, for a table).

### DataTable, TableHeader, TableHeaderCell, TableRow, TableCell
Rows are laid out on a CSS grid so one set of markup serves phone and desktop.
- `<DataTable Label="...">` wraps the table (screen readers read the label).
- `<TableHeader Columns="..." ColumnsWide="...">` and every `<TableRow Columns="..." ColumnsWide="...">` get the same two values: `Columns` is the phone grid (for example `44px minmax(0, 1fr) 64px 52px`), `ColumnsWide` the grid at 900 px and wider.
- A cell with `HidePhone="true"` is not shown on phones; the phone `Columns` leave it out.
- `TableHeaderCell`: `Text`, `End` (right-align), `HidePhone`, `Hint` (hover text), and for sorting `OnSort`, `SortActive`, `Ascending`.
- `TableRow`: `State` (`Normal`, `Selected` for the player's own row, `Pending`, `Disabled`), `Href` (row is a link) or `OnClick` (row is a button), `AriaLabel` (what a screen reader says for a link or button row).
- `TableCell`: `Kind` (`Text`, `Rank`, `Name`, `Points`, `Muted`) and `End`. A `Name` cell can hold a `<span>` followed by `Badge`s.

### Badge
Small marker: `Neutral` ("out"), `Live`, `Good` ("paid"), `Alert` ("unpaid").

### PrimaryButton and SecondaryButton
Orange (the one main action on a screen) and white with a border (everything else). Parameters: `Icon`, `IconOnly` (the words stay as the accessible name), `Href` (makes it a link; an address under `/.auth/` is loaded by the browser, not routed inside the app, or it ends at "Page not found"), `OnClick`, `Disabled`, `FullWidth`, `Large` (56 px, for the action at the bottom of a screen), `ButtonType`, `AriaLabel`.

### Notice
A message box: `Tone` is `Info`, `Tip`, `Success`, `Warning` or `Error`, with an optional `Title`. Errors and warnings are announced to screen readers.

### EmptyState and LoadingIndicator
`EmptyState`: `Title`, `Icon`, the explanation as `<ChildContent>`, and the next step in `<Action>` (a button). `LoadingIndicator`: a spinner with a short `Text`. When a component has both `ChildContent` and a named fragment such as `Action`, write `<ChildContent>` explicitly.

### TeamPickButton
The big team button for picks. `Team` (for the logo, using `TeamLogo`), `Name`, `ApRank` (shown "#15"), `Seed` (shown "(7)"), `SubText`, `OnClick`, and `State`: `Normal`, `Selected`, `Disabled` (team not decided yet; use `ShowLogo` and a name such as "Winner of Rose Bowl"), `Error` (a pick is missing), `Correct` and `Wrong` (final game). Put two per row in a two-column grid on phones.

### PoolSwitcher
Draws the pool choice from `PoolSelection`: `PoolSwitcherMode.HeaderTabs` (once, in `AppHeader`) on phones and `PoolSwitcherMode.Segmented` (in a page's `PageHeader` actions) on wide screens. Shown only when there is more than one pool.

### BottomNav
The four phone tabs. Only `MainLayout` uses it. A `LiveKeys` entry puts a red dot on a tab (Scores while a game is being played).

## Pick screens (Release 2 group C)
Page-level components in `Client/Components`, built from the shared set and not part of it:
- `GamePickCard`: one game on My Picks (away team first, home second), with locked (right or wrong once final) and "pick cleared" states.
- `PickProgress`: "12 of 34 picked" with a bar.
- `StickySaveBar`: keeps the save button on screen, just above the phone bottom navigation.
- `CompactPickButton`: the 44 px team button in the paper-entry table.
- `PickSequenceInput`: the fast-entry digit box (1 = first team listed, 2 = second, 0 = skip).

Game order for picks and print is `GameOrder.ForEntry` (Core): bowl games by kickoff, then playoff games by kickoff. The digit logic is `FastEntry` (Core, xUnit tests).

## Adding to the set
1. Write the component in `Client/Ui/` with a short comment per parameter.
2. Add its states to `Pages/Admin/ComponentSheet.razor`.
3. Add a section here.
4. Keep logic that is not drawing in Core with xUnit tests (no bUnit this season).
