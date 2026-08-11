# shared-mutable-concurrency — Main

## What To Do Now
Give each unit owned state, or route updates through one writer/actor/channel. Remove ad hoc lock webs around shared mutable graphs.

## Repair Strategy
Identify shared mutable locations. Choose ownership, CSP/actor, or STM/single writer. Move invariants next to the owner.

## Decision Branches
If performance requires shared structures, use proven concurrent types with clear invariants and tests—not hand-rolled lock ordering.

## Wrong Fixes
Sprinkling mutexes until races become rare. Cloning entire graphs on every read without an ownership story. Documenting "do not call concurrently" on a hot shared API.

## Verification
Stress concurrent paths; invariants hold without lock-order deadlocks. Design review shows a single mutation owner or message protocol.

## Done When
Concurrent coordination uses ownership or messages; ad hoc shared mutation is gone from the hot path.

## Scope and Authority
Multi-worker mutable coordination. Not single-threaded pure pipelines.
