# recovery-by-filesystem-state — Main

## What To Do Now
Write an explicit lifecycle record (status, step, cursor, epoch) to the journal or store. Recover by reading that fact, not by scanning leftover paths.

## Repair Strategy
Enumerate stages the FS scrape currently implies. Replace each scrape with a committed state transition. Delete reliance on temp file presence as a signal.

## Decision Branches
If migrating old residue-based recovery, one-shot import residue into facts, then disable path sniffing. If only crash markers exist, convert them into typed recovery events.

## Wrong Fixes
Checking whether a lock file exists as the sole progress bit. Treating partial directories as committed stages. Rebuilding policy from glob patterns.

## Verification
Simulate crash mid-stage; recovery reaches the same state from the lifecycle record with residue deleted or rearranged.

## Done When
Recovery path reads durable lifecycle facts; incidental FS layout is irrelevant to progress.

## Scope and Authority
Workflow/session recovery and resumable jobs. Not ordinary config file loading.
