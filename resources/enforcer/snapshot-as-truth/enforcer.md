# snapshot-as-truth — Enforcer

## Definition
A snapshot becomes false authority when a cache, projection, summary, or checkpoint is treated as the original fact source rather than as a disposable acceleration of facts owned elsewhere. The root-cause is that a derived compression is promoted to truth, so staleness or corruption becomes indistinguishable from legitimate state and the independent evidence needed to challenge it is lost.

## Governing Principle
A snapshot compresses history by forgetting how the current value was derived. That is why it is useful and why it is weaker evidence. If the projection is promoted to truth, the system loses the independent facts needed to rebuild or reject it. A bookmark may accelerate reading; it must not rewrite the book.

## Trigger When
Trigger when recovery or business decisions trust a snapshot or projection even when it disagrees with or cannot be validated against the authoritative facts from which it should derive.

## Do Not Trigger When
- The snapshot is explicitly the system of record by contract and no stronger underlying fact history exists.
- The projection is used only as a cache and is discarded or rebuilt on mismatch with the source.
- Read models are clearly labeled derived and writes always go to the source facts.
- A test inspects a snapshot as an observation of a rebuild, not as an independent authority.

## Distinguish From
`duplicated-truth` allows several writable authorities for one present fact. `recovery-by-filesystem-state` infers progress from residue. This rule specifically elevates a derived representation over its source facts. Tie-break: if a checkpoint or projection outranks its source, this rule owns the case.

## Decision Procedure
1. Ask whether the snapshot can be deleted and rebuilt without losing semantic information.
2. If yes, it is derivative and must remain subordinate to the facts that reconstruct it.
3. Record enough identity—count, version, digest, source position—to prove which fact prefix it represents.
4. On mismatch, reject the snapshot and replay from a trusted point.

## Examples
- positive: crash recovery loads `checkpoint.bin` even when its digest disagrees with the event log, and that checkpoint becomes current state.
- near-miss: a materialized view is the documented system of record with no event log behind it.
- counterexample: on digest mismatch the snapshot is deleted and state is replayed from the log.

## Nudge
A snapshot is a bookmark, not testimony. Validate or rebuild it from authoritative facts and never let an optimization become a competing source of history.
