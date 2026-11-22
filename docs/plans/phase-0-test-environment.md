# Phase 0: test environment and test baseline

Status: ready to start. Written 2026-10-29. Part of docs/UPGRADE-PLAN-2026.md (section 3 "Before Release 1" and section 13).

## Start here (for a new session)
1. Read CLAUDE.md, then docs/UPGRADE-PLAN-2026.md sections 3 and 13, then this file.
2. Read docs/INFRASTRUCTURE.md ("Deploys and PR previews", "App settings", "Reading the logs").
3. Check the "Progress" section at the bottom of this file and continue from the first step not marked done. Update it as steps finish.

## Goal
A sister test site with its own database, so every later phase is tested end to end before production, and a test baseline that protects the .NET 10 upgrade. Must be done in time for Phase 1.0 to merge before 2026-11-10 (target: Phase 0 done by 2026-11-03).

## Out of scope
- The score feed mock (Phase 1.1, group A).
- Fixing any filed bug. Where a baseline test exposes a known bug, the test records today's behavior and names the issue (see step 4).

## Decisions already made (do not reopen)
- Sister test site: a second Free static web app, `swa-bowlmadness-test`, in its own resource group `rg-bowlmadness-test`, Central US.
- Its own Cosmos DB account, `cosmos-bowlmadness-test`, **serverless** (pay per use), database `BowlMadnessDb`, same five containers and partition keys as production. No free tier (it leaves the subscription's one free-tier slot unused).
- The test site does not get `CfbdApiKey`; Phase 1.1 adds a mock feed.
- Test-only features run only when `SiteEnvironment=Test` is set and the database is not the production account.
- Seed data comes from fixtures in the repo, never copied from production.

## Owner decisions (2026-11-22)
1. Branch flow: A, a `staging` branch.
2. Known failing tests in CI: A, the `KnownFailure` trait.
3. Browser telemetry on the test site: B, accept the noise (no change to `index.html`).

## Decisions needed from the owner before the steps that use them (decided above)
1. **Branch flow for releases (before step 2).** Merging to main deploys production, and Release 1.1 is one release made of several PRs. Options:
   - **A (recommended): a `staging` branch.** Feature PRs target `staging`; each PR gets a test-site preview; merging into `staging` updates the test site's main environment; a release PR from `staging` to `main` ships to production. Release PRs merge with a merge commit, not squash, and never with `--delete-branch` (so `staging` survives and history stays in step). This changes the merge rule in CLAUDE.md, which the owner edits.
   - B: feature PRs target `main` as today and each merge ships to production. Simpler, but 1.1 would reach production in pieces.
2. **Known failing tests in CI (before step 2).** The two #94 tests fail today, so a CI test gate would always fail. Options:
   - **A (recommended):** mark the two tests with `[Trait("Category", "KnownFailure")]` and a comment naming #94; CI runs `--filter "Category!=KnownFailure"`; Phase 1.1 group C removes the trait when #94 is fixed.
   - B: fix #94 first (it may be a scoring bug, so it's not a small change).
3. **Browser telemetry on the test site (step 2).** `wwwroot/index.html` hard-codes production's Application Insights connection string, so test-site page views would land in production telemetry. Options:
   - **A (recommended):** load the browser snippet only when the host is a production host (bowl-madness.com, hoops-madness.com, or the production default host).
   - B: accept the noise.

## Steps

### Step 1: create the Azure resources (owner approval required)
Ask the owner before running anything. The hook asks before some `az` commands; if it refuses one, report the rule and stop.
```powershell
az group create -n rg-bowlmadness-test -l centralus
az cosmosdb create -n cosmos-bowlmadness-test -g rg-bowlmadness-test --locations regionName=centralus --capabilities EnableServerless --default-consistency-level Session
az cosmosdb sql database create -a cosmos-bowlmadness-test -g rg-bowlmadness-test -n BowlMadnessDb
foreach ($c in @(@("Players","/id"),@("Seasons","/seasonId"),@("Picks","/seasonId"),@("PoolArchives","/seasonId"),@("Configuration","/id"))) { az cosmosdb sql container create -a cosmos-bowlmadness-test -g rg-bowlmadness-test -d BowlMadnessDb -n $c[0] -p $c[1] }
az staticwebapp create -n swa-bowlmadness-test -g rg-bowlmadness-test -l centralus --sku Free
```
Production's legacy `MainContainer` is left out; only the migration tools use it.

**Secrets: the owner runs these.** The repo hook (rule R2b) refuses commands that read keys, tokens or app settings, and Claude must not see them. Give the owner these commands to run in their own terminal:
```powershell
$cs = az cosmosdb keys list -n cosmos-bowlmadness-test -g rg-bowlmadness-test --type connection-strings --query "connectionStrings[0].connectionString" -o tsv
az staticwebapp appsettings set -n swa-bowlmadness-test -g rg-bowlmadness-test --setting-names "CosmosDbConnectionString=$cs" "BootstrapAdminEmail=<owner email>" "SiteEnvironment=Test"
az staticwebapp secrets list -n swa-bowlmadness-test -g rg-bowlmadness-test --query "properties.apiKey" -o tsv | gh secret set AZURE_STATIC_WEB_APPS_API_TOKEN_TEST
```
Verify without reading values: `az group show`, `az cosmosdb show --query "{name:name, caps:capabilities}"`, `az cosmosdb sql container list --query "[].name"`, `az staticwebapp show --query "{name:name, sku:sku.name, host:defaultHostname}"`, `gh secret list` (names only). Record the test site's default host in docs/INFRASTRUCTURE.md.

### Step 2: workflows (branch and PR)
- New workflow `.github/workflows/test-site.yml`:
  - Triggers: pull requests (opened, synchronize, reopened, closed) and pushes for the branches chosen in decision 1.
  - Job 1: set up .NET 10, `dotnet build BowlPoolManager.sln`, `dotnet test` with the filter from decision 2.
  - Job 2 (needs job 1): `Azure/static-web-apps-deploy@v1` with `AZURE_STATIC_WEB_APPS_API_TOKEN_TEST`, same app/api/output locations as production. Close job for closed PRs.
- Production workflow: deploy only on push to `main` (remove the pull_request trigger and the close job), and add the same build-and-test job in front of the deploy.
- If decision 1 is A: create the `staging` branch from `main` (ask before pushing it).
- Verify: the PR's own run deploys a test-site preview; production gets no preview. Record the preview address format in docs/INFRASTRUCTURE.md.

### Step 3: test-only guard, seed and reset (branch and PR, tests first)
- `TestEnvironment` helper in Api: true only when `SiteEnvironment` equals `Test` and the Cosmos account endpoint is not `cosmos-bowlmadness-prod`. Unit tests for both conditions.
- SuperAdmin function `ResetTestData` (POST): checks the caller first, then the guard (403 if not test). Deletes everything in Seasons, Picks and PoolArchives, keeps Players (so the owner stays SuperAdmin), then loads a named scenario. Returns counts.
- Scenarios, as JSON fixtures embedded in the Api project with made-up names:
  - `before-selection-day`: current season, Configuration team lists, no pools.
  - `entry-window`: plus Family and Office pools, the bowl and playoff games (15 bowls, 4 first-round, 4 quarterfinal, 2 semifinal, championship) with sample teams, linked bracket, and about 30 entries, some incomplete.
  - `mid-season`: plus about half the games final, one in progress.
  Later phases add `last-season-replay` (Phase 1.1 group C).
- Admin page "Test data" (hidden unless the API says test mode is on): pick a scenario, confirm, show counts.
- Verify on the test site: sign in, become SuperAdmin through `BootstrapAdminEmail`, load each scenario, check the leaderboard, scoreboard, My Picks and Path to Victory show it.

### Step 4: baseline tests (branch and PR; can run alongside step 3)
- **Serialization round-trips** for every model stored in Cosmos (BowlGame, BowlPool, BracketEntry, Season, UserProfile, PoolArchive, HoopsGame, HoopsPool, TeamModels, config documents): save and reload with Newtonsoft (as Cosmos does, camelCase policy) and with System.Text.Json; property names match; sample JSON captured from today's format still loads.
- **Endpoint sign-in and role tests** for every write endpoint (table in the 2026 security review, summarized in #93 and #97): anonymous gets 401, a player gets 403 where the rule says so, an admin or SuperAdmin succeeds. Use the pattern in `HoopsArchiveFunctionsTests` (fake request and repositories).
  - Where today's behavior breaks a rule (for example `SaveSeason` and `GetBackupData` are anonymous, #93), write the test for the correct behavior, mark it `[Trait("Category", "KnownFailure")]` with the issue number, and leave it failing. The fix in Phase 1.1 removes the trait.
- Small: `DateTimeHelper` around daylight-saving changes; `SecurityHelper` with a malformed header.
- Name tests `MethodName_ShouldExpectedBehavior_WhenCondition`; `It.IsAny<T>()` in Moq setups.

### Step 5: documentation (owner approves each edit)
- docs/INFRASTRUCTURE.md: test resources, test host, test secret name, workflows, `SiteEnvironment`.
- ARCHITECTURAL_CONTEXT.md: test environment, guard, scenarios.
- CLAUDE.md (owner edits): remove "A PR preview appears to use the production database", describe the test site, and (if decision 1 is A) the staging branch and release-merge rule.
- docs/UPGRADE-PLAN-2026.md: mark Phase 0 done.

## Done when
- Test resources exist; secrets set by the owner; nothing in production changed except the workflow.
- PRs deploy to the test site, not to production previews; production deploys only from main; tests run before every deploy and block it on failure.
- The guard, reset and three scenarios work on the test site, and refuse to run against production (tested).
- Serialization and endpoint tests merged; known failures are labelled with their issues; the rest of the suite passes.
- Docs updated and approved.

## Risks
- The test site is on a Free plan with 3 preview environments; close PRs promptly.
- The first deploy of the test site happens before its app settings exist if step 2 runs before the owner sets secrets. Do step 1's secrets first.
- Signing in on the test site uses the same Google and Microsoft providers; the owner becomes SuperAdmin there only through `BootstrapAdminEmail`.

## Progress
- [x] Decisions 1-3 (2026-11-22)
- [ ] Step 1 resources
- [ ] Step 1 secrets (owner)
- [ ] Step 2 workflows
- [ ] Step 3 guard, seed, reset
- [ ] Step 4 baseline tests
- [ ] Step 5 docs
