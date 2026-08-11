# memory-before-disk — Enforcer

## Definition
Memory is updated before disk when authoritative runtime state begins reflecting a fact before the durable record that justifies that fact has committed.

## Governing Principle
Volatile state may be fast, but after a crash only durable history gets to testify about what happened. If memory moves first, the running process can observe a future that recovery cannot reconstruct. Correct ordering therefore follows epistemic authority: durable commitment establishes the fact; memory is merely a projection permitted to advance afterward.

## Trigger When
Trigger when command handling mutates authoritative in-memory state, publishes the new state, or performs dependent work before the event/journal/transaction establishing that transition is durably committed.

## Do Not Trigger When
- Do not trigger when memory is explicitly speculative and cannot escape or influence authoritative behavior before the durable commit succeeds.
- Do not trigger for caches explicitly derived from durable state and discarded on restart.
- Do not trigger when a write-ahead buffer is itself the durable medium (fsync’d WAL) and recovery reads it.

## Distinguish From
blob-after-event orders durable references and content. overwrite-history mutates committed past facts. This rule concerns volatile authority outrunning durable evidence. Tie-break: if memory is treated as truth before commit, use this rule; if a durable past fact is rewritten, use overwrite-history.

## Decision Procedure
Crash the process at every boundary. If any externally visible or authoritative memory state can survive long enough to influence behavior while its justifying fact would vanish on restart, the order is wrong.

## Examples
- positive: The in-memory aggregate is updated, then the journal write is attempted; a crash leaves observers ahead of recovery.
- near-miss: A speculative buffer is held privately and dropped if commit fails; nothing authoritative escaped.
- counterexample: The journal commits first; memory advances only from the committed result.

## Nudge
Memory may project history; it must not precede history. Commit the fact first, then advance authoritative runtime state from the committed result.
