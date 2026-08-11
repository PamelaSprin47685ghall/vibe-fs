# duplicated-truth — Main

## What To Do Now
Declare one canonical writable representation for the fact and turn every other copy into a deterministic projection, cache, or view of that source.

## Why This Matters
When two places can both define the same fact, inconsistency is no longer a bug in synchronization—it is a state the architecture permits. Every repair mechanism then needs precedence rules, timestamps, reconciliation, or manual judgment because the model refused to answer where truth lives.

## Repair Strategy
Trace all writers first. Remove duplicate write authority, then rebuild secondary forms from the owner through explicit projection or synchronization that has one direction. Preserve cache invalidation as an implementation concern, never as a second source of truth.

## Wrong Fixes
Do not add “last write wins” merely to settle conflicts you could make impossible. Do not call one copy “primary” while allowing the other to overwrite it.

## Verification
Create conflicting hypothetical values. The architecture should make one of those writes impossible or clearly subordinate, not require a runtime tie-breaker.

## Done When
Every fact has one answer to “where is this defined?”, while all other representations are visibly derivative and disposable.
