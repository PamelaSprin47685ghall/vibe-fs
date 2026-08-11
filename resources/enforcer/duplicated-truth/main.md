# duplicated-truth — Main

## What To Do Now
Declare one canonical writable representation for the fact and turn every other copy into a deterministic projection, cache, or view of that source. The declared source is who owns the fact; caches and views are not who owns writes.

## Why This Matters
When two places can both define the same fact, inconsistency is no longer a bug in synchronization—it is a state the architecture permits. Every repair mechanism then needs precedence rules, timestamps, reconciliation, or manual judgment because the model refused to answer where truth lives.

## Repair Strategy
Trace all writers first. Remove duplicate write authority, then rebuild secondary forms from the owner through explicit projection or synchronization that has one direction. Preserve cache invalidation as an implementation concern, never as a second source of truth.

## Decision Branches
- If two stores can both write the fact, pick one authority and make the other a derived projection.
- If a cache can be rebuilt, keep it read-only and disposable; never let a cache miss invent a write.
- If a snapshot is being treated as live truth, that is `snapshot-as-truth`; demote the snapshot rather than adding a tie-breaker.

## Common Wrong Fixes
- Do not add “last write wins” merely to settle conflicts you could make impossible.
- Do not call one copy “primary” while allowing the other to overwrite it.
- Do not add bidirectional sync “to keep them equal.”
- Do not paper over disagreement with timestamps or operator dashboards.

## Verification
Create conflicting hypothetical values. The architecture should make one of those writes impossible or clearly subordinate, not require a runtime tie-breaker. The invariant is that every fact has one answer to “where is this defined?”

## Done When
Every fact has one answer to “where is this defined?”, while all other representations are visibly derivative and disposable.
