# snapshot-as-truth — Main

## What To Do Now
Treat snapshots as disposable projections. Recover and reconcile from the authoritative event/fact log. Rebuild caches instead of editing them as source.

## Repair Strategy
Identify the authority. Make snapshot rebuild a pure fold over facts. Stop writing business corrections only into the cache.

## Decision Branches
If facts were lost, perform an explicit repair import that re-establishes authority—do not quietly anoint the cache forever.

## Wrong Fixes
Hand-editing Redis to "fix production". Using a reporting table as the write model. Skipping event replay because the snapshot "looks fine".

## Verification
Delete or corrupt the snapshot; rebuild from facts restores the same projection. Authority remains the fact stream.

## Done When
Snapshots are derived and rebuildable; no business path treats them as the original source.

## Scope and Authority
Caches, read models, and summaries over an authoritative fact log.
