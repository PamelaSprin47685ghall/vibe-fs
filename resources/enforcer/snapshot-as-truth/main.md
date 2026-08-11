# snapshot-as-truth — Main

## What To Do Now
Restore the snapshot to derivative status: validate its provenance, discard it when inconsistent, and rebuild current state from the authoritative facts.

## Why This Matters
Snapshots deliberately discard information to make recovery fast. Treating them as stronger than the history they summarize turns that lossiness into authority. A stale or corrupt checkpoint can then overwrite evidence rather than merely delay startup.

## Repair Strategy
Record enough identity—event count, version, digest, source position—to prove which fact prefix the snapshot represents. On mismatch, reject the snapshot and replay from a trusted point.

## Wrong Fixes
Do not compare only file timestamps or sizes and assume alignment. Incidental metadata is not proof that two semantic histories correspond.

## Verification
Corrupt, stale, or swap a snapshot while preserving the fact source. Recovery must detect the mismatch and converge to the state obtained by replaying authoritative facts.

## Done When
Snapshots can accelerate reconstruction but cannot change truth: deleting every snapshot may cost time, never semantic information.
