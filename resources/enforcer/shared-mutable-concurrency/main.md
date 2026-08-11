# shared-mutable-concurrency — Main

## What To Do Now
Choose a single owner for mutable domain state and move concurrent participants to message passing, immutable snapshots, or serialized commands.

## Why This Matters
Locks distribute the proof of correctness across every access path: which lock, in what order, around which compound invariant. Ownership concentrates that proof. The owner sees one change at a time; everyone else communicates intent rather than reaching into the state directly.

## Repair Strategy
Place mutation behind an actor/queue/single-writer boundary, send typed commands inward, and publish immutable results outward. Keep unavoidable shared concurrent structures narrow and limited to semantics they natively guarantee.

## Wrong Fixes
Do not merely add a larger global lock. That may remove races while preserving one giant shared authority and replacing concurrency bugs with contention or deadlock risk.

## Verification
Concurrent callers should be unable to mutate owned state directly, and varying scheduler order must preserve declared command semantics.

## Done When
Concurrency exists between owners, not inside one shared mutable object, and synchronization follows the domain’s authority boundaries rather than ad hoc lock placement.
