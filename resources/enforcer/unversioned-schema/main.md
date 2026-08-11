# unversioned-schema — Main

## What To Do Now
Add an explicit schema version to the durable contract and define deterministic read, migration, and rejection behavior for every supported version.

## Why This Matters
Persisted data outlives the code that wrote it. Without a version, future readers must infer historical meaning from field shape, making compatibility dependent on coincidence and heuristics precisely when the original writer is gone.

## Repair Strategy
Version at the semantic boundary, not merely by deployment number. Keep migrations pure and test them with historical fixtures; reject unknown future versions rather than best-effort parsing them into current meaning.

## Wrong Fixes
Do not detect versions from field presence or filenames if an explicit marker can be stored. Shape is evidence only until two historical schemas share it.

## Verification
Every supported historical fixture should deterministically migrate or read, and unknown versions should fail with a typed compatibility outcome.

## Done When
Any durable value carries enough evidence for future code to know which schema semantics produced it before interpretation begins.
