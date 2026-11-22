# Phase 1.0: .NET 10 upgrade

Status: waits for Phase 0. Written 2026-10-29. Part of docs/UPGRADE-PLAN-2026.md (section 3, Release 1.0).

## Start here (for a new session)
1. Read CLAUDE.md, docs/UPGRADE-PLAN-2026.md section 3, docs/plans/phase-0-test-environment.md ("Done when" and "Progress"), then this file.
2. Confirm Phase 0 is done: the test site exists, PRs deploy there, and tests run in CI. If not, stop and tell the owner.
3. Check "Progress" at the bottom and continue from the first open step.

## Goal
Move the API and shared code to .NET 10 so the site stays on a supported runtime, and change nothing else. Hard deadline: in production before **2026-11-10**, when .NET 8 and 9 support ends on Static Web Apps managed functions. Target merge: 2026-11-06, leaving room to fix or roll back.

## Out of scope
Every bug fix, refactor and package change that the upgrade doesn't need. If something breaks only because of the upgrade, fix it here; anything else is reported and goes to its issue.

## Facts
- Client and Tests already target net10.0. Core and Api target net8.0 (`BowlPoolManager.Core.csproj:4`, `BowlPoolManager.Api.csproj:4`).
- Api packages today: Microsoft.Azure.Functions.Worker 2.51.0, Worker.Sdk 2.0.7, Worker.ApplicationInsights 2.50.0, Worker.Extensions.Http.AspNetCore 2.1.0, Microsoft.ApplicationInsights.WorkerService 2.23.0, Microsoft.Azure.Cosmos 3.56.0, Newtonsoft.Json 13.0.4.
- `staticwebapp.config.json` sets no `platform.apiRuntime` today, so Azure picks the runtime. Microsoft documents `dotnet-isolated:10.0` as supported (checked 2026-10-28). Re-check the "API support" / configuration docs before relying on it.
- The 2026 review found no Api or Core code that obviously breaks on .NET 10. Watch serialization, time zones (`DateTimeHelper`), and the Functions host builder in `Program.cs`.

## Steps
1. **Baseline.** On a branch `upgrade/dotnet10`, run the full build and tests on unchanged code and record the result (expected: all pass except the two #94 tests, which Phase 0 labels as known failures).
2. **Retarget.** Core and Api to `net10.0`. Build. Fix only what the compiler requires.
3. **Packages.** For each Api package, find the newest stable version that supports .NET 10 isolated worker (check the package's release notes; don't guess versions). Upgrade only those that need it for .NET 10, plus the Functions SDK and worker as Microsoft's migration guide says. List each change and why.
4. **Runtime setting.** Add `"platform": { "apiRuntime": "dotnet-isolated:10.0" }` to `BowlPoolManager.Client/wwwroot/staticwebapp.config.json`.
5. **Warnings.** Compare build warnings before and after; anything new from the upgrade is fixed or explained.
6. **Tests.** Full suite. The Phase 0 serialization and endpoint tests are the main safety net; they must pass unchanged (apart from labelled known failures).
7. **Review.** Run the code-review agent (and security-data-review only if a change touches auth or data). Fix critical and high findings.
8. **Test site.** Open the PR (ask first). It deploys to the test site. Load the `entry-window` and `mid-season` scenarios and check by hand:
   - leaderboard, scoreboard, Path to Victory, History, Print bracket;
   - sign in, create and save an entry, edit it;
   - admin: entries list, payments, game results (set a game final, check propagation), pool settings (don't click New Pool; that crash is #129, fixed in 1.1);
   - the API reports .NET 10 (`/api/healthcheck` if it returns the version; otherwise the deploy log's runtime line).
   Use the deployment-validator agent for a read-only smoke test of the preview.
9. **Release.** Summarize what changed, what could go wrong and what was tested; ask the owner to merge. Follow CLAUDE.md for the merge and for a release through `staging` if Phase 0 chose that flow.
10. **Production check.** Read-only: the deployment-validator agent against production, then "Reading the logs" in docs/INFRASTRUCTURE.md for errors in the first hour.
11. **Docs** (owner approves): ARCHITECTURAL_CONTEXT.md sections 1 and 9 (target frameworks, runtime setting), CLAUDE.md if it names versions, docs/UPGRADE-PLAN-2026.md progress.

## Rollback
Revert the merge with a new PR (never force-push or reset). Before 2026-11-10 the .NET 8 runtime still runs; after that, rolling back is not an option for long, which is why the target merge is 2026-11-06.

## Done when
Core and Api on net10.0; `apiRuntime` set; tests pass (known failures labelled); the test site checklist passed; production deployed and healthy for an hour with no new errors in the logs; docs updated.

## Progress
- [ ] 1 Baseline
- [ ] 2 Retarget
- [ ] 3 Packages
- [ ] 4 Runtime setting
- [ ] 5 Warnings
- [ ] 6 Tests
- [ ] 7 Review
- [ ] 8 Test site
- [ ] 9 Release
- [ ] 10 Production check
- [ ] 11 Docs
