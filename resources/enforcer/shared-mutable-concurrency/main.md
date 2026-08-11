# shared-mutable-concurrency — Main

## What To Do Now
Choose a single owner for mutable domain state and move concurrent participants to message passing, immutable snapshots, or serialized commands. That owner is who owns mutation; other workers own only the messages or snapshots they send and receive.

## Why This Matters
Locks distribute the proof of correctness across every access path: which lock, in what order, around which compound invariant. Ownership concentrates that proof. The owner sees one change at a time; everyone else communicates intent rather than reaching into the state directly.

## Repair Strategy
Place mutation behind an actor, queue, or single-writer boundary, send typed commands inward, and publish immutable results outward. Keep unavoidable shared concurrent structures narrow and limited to semantics they natively guarantee.

## Decision Branches
- If several workers mutate one domain object, introduce a single writer and turn others into command senders.
- If a standard concurrent structure already matches the needed atomic, keep it narrow and do not expose compound fields around it.
- If the sharing is only reads of immutable snapshots, leave it; this rule is about write authority.

## Common Wrong Fixes
- Do not add a larger global lock around the same shared object.
- Do not sprinkle more synchronized blocks without naming an owner.
- Do not make every field atomic and hope compound invariants hold.
- Do not document “remember to take the lock” as the architecture.

## Verification
Concurrent callers should be unable to mutate owned state directly, and varying scheduler order must preserve declared command semantics. The invariant is that mutation authority is singular; concurrency communicates across that boundary.

## Done When
Concurrency exists between owners, not inside one shared mutable object, and synchronization follows the domain’s authority boundaries rather than ad hoc lock placement.
