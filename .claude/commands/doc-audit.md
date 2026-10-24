---
description: Read-only audit of CLAUDE.md, ARCHITECTURAL_CONTEXT.md and docs/BUSINESS-RULES.md against the code, with business-rules coverage tables. Changes nothing.
disable-model-invocation: true
allowed-tools: Read, Grep, Glob, Bash(git log:*), Bash(git ls-files:*), Bash(gh issue list:*), Bash(gh issue view:*), PowerShell(git log:*), PowerShell(git ls-files:*), PowerShell(gh issue list:*), PowerShell(gh issue view:*)
---

Compare the project docs with the code. This is read-only: change no file, file no issue, run no build. Use PowerShell syntax. Every row needs file evidence (path and line or function). If you cannot find evidence, say "unverifiable" and do not guess.

Read CLAUDE.md, ARCHITECTURAL_CONTEXT.md and docs/BUSINESS-RULES.md first, then the open issues (`gh issue list --state open --limit 100`). Also read each issue's comments (`gh issue view <n> --comments`), because some deviations are recorded only in comments.

## Table 1: documentation claims

| Doc and section | Claim | Code evidence (file:line) | Status |
|---|---|---|---|

Status is match, stale, wrong or unverifiable. Check every concrete claim: project names and target frameworks, containers and partition keys, function names and routes, helper names, the caller-check pattern, serialization rules, the date rules, id formats.

## Table 2: business-rules coverage

One row per rule in docs/BUSINESS-RULES.md (each bullet in sections 1 to 7).

| Section | Rule | Where enforced (file, function) | Test that covers it | Open issue | Status |
|---|---|---|---|---|---|

Status is enforced, partly enforced, not enforced or unverifiable. Check enforcement on the server, not only in the client. Check football and hoops separately where the rule covers both.

## Table 3: behavior in code that no doc describes

| Behavior | File and function | Which doc should hold it |
|---|---|---|

Look at permissions, routes, containers, id formats, settings and scheduled or lazy behavior.

## Table 4: structure problems

Facts stated in more than one doc, and facts in the wrong doc. The home for each fact is: rules in CLAUDE.md and BUSINESS-RULES, architecture in ARCHITECTURAL_CONTEXT, status in GitHub Issues.

## Finish

Give a plain-language summary of the main gaps, then list the doc edits you propose, as exact text, for my approval. Do not apply them. Do not propose changes to business rules on your own; report code that disagrees with them as deviations and say whether an open issue covers each.
