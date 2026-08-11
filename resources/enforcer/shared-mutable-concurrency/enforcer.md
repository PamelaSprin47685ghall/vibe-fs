# shared-mutable-concurrency — Enforcer

## Definition
Shared mutable concurrency coordinates workers by letting several execution contexts directly mutate the same state and relying on locks or timing discipline to keep those mutations coherent.

## Governing Principle
The hard part of concurrency is not parallel execution; it is shared authority over change. A lock protects moments, not meaning: readers must still determine which fields belong to one invariant, which lock protects them, and which lock order avoids deadlock. Single ownership reverses the problem. One actor/writer owns mutation; others exchange messages or immutable values, so serialization follows authority rather than being reconstructed from lock choreography.

## Trigger When
Trigger when concurrent workers read and mutate shared domain state under ad hoc locks, especially when multiple locks or compound invariants are involved.

## Do Not Trigger When
- One owner serializes mutation and others only send commands or read immutable snapshots.
- A well-defined concurrent data structure provides the exact atomic semantics required without exposing compound mutable invariants.
- The code is single-threaded with no concurrent mutators.
- Sharing is limited to immutable values or copy-on-write snapshots.

## Distinguish From
in-place-mutation is the general mutation smell. lost-update is one concrete conflict. race-first-wins-semantics concerns timing choosing business truth. This rule is shared write authority itself. Tie-break: fire here when several workers may write the same state; fire in-place-mutation when mutation is sequential; fire lost-update for a specific overwrite conflict; fire race-first-wins-semantics when first completion, not shared mutation, chooses the result.

## Decision Procedure
Identify the state’s semantic owner. If several workers can independently mutate it, ask whether ownership can be centralized and communication expressed as commands/messages or immutable snapshots.

## Examples
- positive: request handlers each lock a shared domain object and mutate several fields that together form one invariant.
- near-miss: an actor mailbox serializes commands against privately owned state.
- counterexample: workers publish immutable events; a single writer folds them.

## Nudge
Remove shared authority before adding smarter locks. Give mutable state one owner and let concurrency communicate across ownership boundaries instead of editing the same world together.
