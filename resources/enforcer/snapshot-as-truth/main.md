# snapshot-as-truth — Main

## What To Do Now
Restore the snapshot to derivative status: validate its provenance, discard it when inconsistent, and rebuild current state from the authoritative facts. The source fact history is who owns truth; the snapshot owns only acceleration.

## Why This Matters
Snapshots deliberately discard information to make recovery fast. Treating them as stronger than the history they summarize turns that lossiness into authority. A stale or corrupt checkpoint can then overwrite evidence rather than merely delay startup.

## Repair Strategy
Record enough identity—event count, version, digest, source position—to prove which fact prefix the snapshot represents. On mismatch, reject the snapshot and replay from a trusted point. Never rebuild the log from the snapshot.

## Decision Branches
- If a stronger fact history exists, treat the snapshot as rebuildable and reject it on provenance mismatch.
- If the snapshot is the contracted system of record, stop calling it a snapshot and stop expecting a hidden stronger log.
- If both snapshot and log are writable, that is `duplicated-truth`—collapse write authority first.

## Common Wrong Fixes
- Do not compare only file timestamps or sizes and assume alignment.
- Do not keep the snapshot on mismatch “because it is newer.”
- Do not rebuild from the snapshot into the log, reversing provenance.
- Do not add a second snapshot for redundancy without a source digest.

## Verification
Corrupt, stale, or swap a snapshot while preserving the fact source. Recovery must detect the mismatch and converge to the state obtained by replaying authoritative facts. The invariant is that deleting snapshots may cost time, never semantic information.

## Done When
Snapshots can accelerate reconstruction but cannot change truth: deleting every snapshot may cost time, never semantic information.
