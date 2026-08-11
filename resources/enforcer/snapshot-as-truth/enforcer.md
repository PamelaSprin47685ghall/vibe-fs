# snapshot-as-truth — Enforcer

## Definition
A snapshot becomes false authority when a cache, projection, summary, or checkpoint is treated as the original fact source rather than as a disposable acceleration of facts owned elsewhere.

## Governing Principle
A snapshot compresses history by forgetting how the current value was derived. That is precisely why it is useful and precisely why it is weaker evidence. If the projection is promoted to truth, corruption or staleness becomes indistinguishable from legitimate state and the system loses the independent evidence needed to rebuild or challenge it. A bookmark may accelerate reading; it must not rewrite the book.

## Trigger When
Trigger when recovery or business decisions trust a snapshot/projection even when it disagrees with or cannot be validated against the authoritative facts from which it should derive.

## Do Not Trigger When
- The snapshot is explicitly the system of record by contract and no stronger underlying fact history exists.
- The projection is used only as a cache and is discarded/rebuilt on mismatch with the source.
- Read models are clearly labeled derived and writes always go to the source facts.
- A test inspects a snapshot as an observation of a rebuild, not as an independent authority.

## Distinguish From
duplicated-truth allows several writable authorities. recovery-by-filesystem-state infers progress from residue. This rule specifically elevates a derived representation over its source facts. Tie-break: fire here when a checkpoint/projection outranks its source; fire duplicated-truth when two stores are both writable authorities; fire recovery-by-filesystem-state when path presence, not a projection, is treated as lifecycle truth.

## Decision Procedure
Ask whether the snapshot can be deleted and rebuilt without losing semantic information. If yes, it is derivative and must remain subordinate to the facts that reconstruct it.

## Examples
- positive: crash recovery loads `checkpoint.bin` even when its digest disagrees with the event log, and that checkpoint becomes current state.
- near-miss: a materialized view is the documented system of record with no event log behind it.
- counterexample: on digest mismatch the snapshot is deleted and state is replayed from the log.

## Nudge
A snapshot is a bookmark, not testimony. Validate or rebuild it from authoritative facts and never let an optimization become a competing source of history.
