---
description: Procedure for any bulk or destructive data operation: analyze, dry-run with counts, wait for approval, execute, verify.
disable-model-invocation: true
argument-hint: [describe the data change]
---

Procedure for a bulk or destructive change to Cosmos data (database BowlMadnessDb). Follow the steps in order. Do not skip ahead. Use PowerShell syntax.

The change to make: $ARGUMENTS

You hold no database credentials. Commands that list keys or connection strings are denied, and you must not look for them. You produce the analysis, the dry-run plan and the approval step. I run or authorize the execution, and I tell you how you may reach the data. If there is no safe read-only way to get counts, say so and stop. Do not invent one.

## Step 0: backup policy

Find the production Cosmos account: `az cosmosdb list -g rg-bowlmadness-prod --query "[].name" -o tsv`. Then run `az cosmosdb show` on it with `--query backupPolicy` (nothing else from that command). If the type is not Continuous, stop and tell me. If it is, continue. A fresh export is optional extra insurance, not a requirement.

## Step 1: analyze

Write down, in plain language:

- what the change does and why
- which containers and which partition keys it touches (Players /id, Seasons /seasonId, Picks /seasonId, PoolArchives /seasonId, Configuration /id)
- which documents it reads, updates or deletes, and how they are selected
- the rules that apply: docs/BUSINESS-RULES.md (archiving drops entry ids, owners, payment status and audit history and cannot be undone; the "2026" season id quirk in section 6; pick visibility; payment status)
- open issues about the same area (`gh issue list --state open --limit 100`). Also read each issue's comments (`gh issue view <n> --comments`), because some deviations are recorded only in comments.
- what could go wrong, and how it would be undone (a point-in-time restore into a new account, see docs/INFRASTRUCTURE.md)

Ask me about intended behavior if BUSINESS-RULES does not answer it. Ask me before any data model change, with two or three options, your recommendation and the tradeoffs. Decide implementation details yourself.

## Step 2: dry run

Prepare a dry run that makes no writes. Code or scripts you write default to dry run and need an explicit flag to write. Every query and write passes the partition key. Report counts per container and per partition: documents that would change, would be deleted, would be skipped, and anything unexpected. Include a small sample of document ids, never picks before lock, secrets or personal data. If counts cannot be obtained without live credentials, say so and stop.

## Step 3: approval

Stop. Show the analysis, the counts and the exact operation. Wait for my explicit yes. My approval covers those counts only. If the real counts differ at execution, stop and report.

## Step 4: execute

Begin by re-running the backup policy check from step 0 (`az cosmosdb show` with `--query backupPolicy`). If the type is not Continuous, stop and tell me. If it is, record the current UTC time immediately before the first write. That time is the restore point. Tell me the time.

Only after my yes, and only the approved operation, through the access path I name. Keep each batch within one partition. Stop at the first unexpected result. Keep a local log of changed document ids outside the repository.

## Step 5: verify

Re-run the count queries. Compare with the dry run and with what the execution reported. Spot-check a few documents. Report every difference in plain language, and state whether the change is complete. Verify within 7 days of the change, because that is the recovery window. If the result is bad, it is recovered with a point-in-time restore into a new account from the restore point recorded in step 4 (see docs/INFRASTRUCTURE.md). Restoring to a new account and re-pointing the app discards any writes made after the restore point.
