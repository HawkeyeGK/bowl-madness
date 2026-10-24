---
description: Report what shipped, what is open and the test state, check for uncommitted work, and propose doc edits. Writes no files.
disable-model-invocation: true
allowed-tools: Read, Grep, Glob, Bash(git status:*), Bash(git branch -vv), Bash(git log:*), Bash(git stash list:*), Bash(git worktree list:*), Bash(git fetch), Bash(gh pr list:*), Bash(gh pr view:*), Bash(gh issue list:*), Bash(gh issue view:*), Bash(dotnet test:*), PowerShell(git status:*), PowerShell(git branch -vv), PowerShell(git log:*), PowerShell(git stash list:*), PowerShell(git worktree list:*), PowerShell(git fetch), PowerShell(gh pr list:*), PowerShell(gh pr view:*), PowerShell(gh issue list:*), PowerShell(gh issue view:*), PowerShell(dotnet test:*)
---

Take stock of the project and report facts. Do not edit any file, do not write to memory, and do not file issues. Use PowerShell syntax.

1. **Shipped.** `gh pr list --state merged --limit 15`, then `gh pr view <n>` for any that matter. List each merged PR with its number, title and what it changed. Report only what the PR and the code show.
2. **Open work.** `gh issue list --state open --limit 100` and `gh pr list --state open`. Group the issues by area. Note any that look stale or already fixed by a merged PR. Also read each issue's comments (`gh issue view <n> --comments`), because some deviations are recorded only in comments.
3. **Test state.** Run `dotnet test BowlPoolManager.Tests/BowlPoolManager.Tests.csproj`. Report tests run, passed and failed. Match each failure to an open issue; list known failures separately from new ones.
4. **Uncommitted and unmerged work.** Run `git status`, `git branch -vv`, `git stash list` and `git worktree list`. Report uncommitted changes, untracked files, commits not pushed, local branches with no remote or already merged, stashes and extra worktrees.
5. **Docs.** Compare what shipped since the docs were last edited (`git log -5 -- ARCHITECTURAL_CONTEXT.md docs/BUSINESS-RULES.md`) with ARCHITECTURAL_CONTEXT.md, docs/BUSINESS-RULES.md and any infrastructure doc. For each place the docs are now wrong or incomplete, propose the exact edit as text. Do not apply it. Do not propose changes to business rules on your own; if the code and the rules disagree, report it as a deviation.
6. **Uncovered problems.** List problems you noticed that no open issue covers, one line each, with a draft issue title. Do not file them.

Finish with a short report: shipped, open, tests, uncommitted work, proposed doc edits, uncovered problems. Then wait for my approval before changing anything.
