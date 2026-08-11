# shared-mutable-concurrency — Enforcer

## Definition
Shared mutable concurrency coordinates workers by letting several execution contexts directly mutate the same state and relying on locks or timing discipline to keep those mutations coherent. The root-cause is that write authority is shared, so correctness is reconstructed from lock choreography instead of from a single owner of change.

## Governing Principle
The hard part of concurrency is not parallel execution; it is shared authority over change. A lock protects moments, not meaning: readers must still determine which fields belong to one invariant, which lock protects them, and which lock order avoids deadlock. Single ownership reverses the problem. One actor or writer owns mutation; others exchange messages or immutable values, so serialization follows authority rather than being reconstructed from locks.

## Trigger When
Trigger when concurrent workers read and mutate shared domain state under ad hoc locks, especially when multiple locks or compound invariants are involved.

## Do Not Trigger When
- One owner serializes mutation and others only send commands or read immutable snapshots.
- A well-defined concurrent data structure provides the exact atomic semantics required without exposing compound mutable invariants.
- The code is single-threaded with no concurrent mutators.
- Sharing is limited to immutable values or copy-on-write snapshots.

## Distinguish From
`in-place-mutation` is the general mutation smell. `lost-update` is one concrete overwrite conflict. `race-first-wins-semantics` concerns timing choosing business truth. This rule is shared write authority itself. Tie-break: if several workers may write the same state, this rule owns the case.

## Decision Procedure
1. Identify the state’s semantic owner.
2. Ask whether several workers can independently mutate it.
3. If yes, centralize ownership.
4. Express communication as commands, messages, or immutable snapshots across that boundary.

## Examples
- positive: request handlers each lock a shared domain object and mutate several fields that together form one invariant.
- near-miss: an actor mailbox serializes commands against privately owned state.
- counterexample: workers publish immutable events; a single writer folds them.

## Nudge
Remove shared authority before adding smarter locks. Give mutable state one owner and let concurrency communicate across ownership boundaries instead of editing the same world together.
