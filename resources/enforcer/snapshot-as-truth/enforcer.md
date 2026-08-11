# snapshot-as-truth — Enforcer

## Definition
A snapshot becomes false authority when a cache, projection, summary, or checkpoint is treated as the original fact source rather than as a disposable acceleration of facts owned elsewhere.

## Governing Principle
A snapshot compresses history by forgetting how the current value was derived. That is precisely why it is useful and precisely why it is weaker evidence. If the projection is promoted to truth, corruption or staleness becomes indistinguishable from legitimate state and the system loses the independent evidence needed to rebuild or challenge it. A bookmark may accelerate reading; it must not rewrite the book.

## Trigger When
Trigger when recovery or business decisions trust a snapshot/projection even when it disagrees with or cannot be validated against the authoritative facts from which it should derive.

## Do Not Trigger When
Do not trigger when the snapshot is explicitly the system of record by contract and no stronger underlying fact history exists.

## Distinguish From
duplicated-truth allows several writable authorities. recovery-by-filesystem-state infers progress from residue. This rule specifically elevates a derived representation over its source facts.

## Decision Procedure
Ask whether the snapshot can be deleted and rebuilt without losing semantic information. If yes, it is derivative and must remain subordinate to the facts that reconstruct it.

## Nudge
A snapshot is a bookmark, not testimony. Validate or rebuild it from authoritative facts and never let an optimization become a competing source of history.
