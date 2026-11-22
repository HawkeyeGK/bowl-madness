# Execution plans

The 2026 upgrade (docs/UPGRADE-PLAN-2026.md) runs in phases. Each phase has a plan here, written so a new session can start from it without the conversation that produced it. Run each phase, or each group inside Phase 1.1, in its own session.

| Phase | Plan | Status | Target |
|---|---|---|---|
| 0: test environment and test baseline | [phase-0-test-environment.md](phase-0-test-environment.md) | Ready to start | Done by 2026-11-03 |
| 1.0: .NET 10 upgrade | [phase-1.0-dotnet10.md](phase-1.0-dotnet10.md) | Waits for Phase 0 | Production before 2026-11-10 (merge target 2026-11-06) |
| 1.1: fixes, groups A-D | [phase-1.1-fixes.md](phase-1.1-fixes.md) | Waits for Phase 1.0 | Production about 2026-11-20 |
| 2: new UI (football player pages, season admin) | Not written yet | Write after Phase 1.0, from the prototypes and plan section 5 | Production about 2026-11-29 |
| 3: hoops | Not written yet | Write after the bowl season | Jan-Feb 2027 |

## Starting a session
1. Tell the session which plan (and for Phase 1.1, which group) to work on.
2. The session reads CLAUDE.md, the sections of docs/UPGRADE-PLAN-2026.md that the plan names, and the plan.
3. It continues from the first open item in the plan's "Progress" list and ticks items off as they finish, in the same branch as the work.

## Where things live
- Decisions and the reasons for them: docs/UPGRADE-PLAN-2026.md (section 10 lists them).
- Intended behavior: docs/BUSINESS-RULES.md.
- Bugs and their status: GitHub Issues. The 2026 review filed #125-#140 and widened #93, #97, #101, #107 with comments.
- UI prototypes (Release 2): the private canvas linked in docs/UPGRADE-PLAN-2026.md section 11. Only the owner can open it; ask them for screenshots or to share it.
