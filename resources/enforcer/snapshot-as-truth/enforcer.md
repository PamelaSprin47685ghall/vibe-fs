# snapshot-as-truth — Enforcer

## Definition
A cache, projection, snapshot, or summary is treated as the original fact source rather than a derived bookmark.

## Trigger When
A cache, projection, snapshot, or summary is treated as the original fact source rather than a derived bookmark.

## Do Not Trigger When
Do not fire when the snapshot is explicitly the authorized system of record by design (rare) and rebuild-from-facts is not applicable.

## Distinguish From
recovery-by-filesystem-state sniffs layout; duplicated-truth keeps two authorities; this tip elevates a derived view over facts.

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
A derived snapshot is being treated as truth. Recover from the authoritative facts and rebuild the projection.

## Examples
### Positive
A cache, projection, snapshot, or summary is treated as the original fact source rather than a derived bookmark.

### Near miss
A related situation that shares vocabulary but does not cross this tip's boundary — see Distinguish From.

### Counterexample
Do not fire when the snapshot is explicitly the authorized system of record by design (rare) and rebuild-from-facts is not applicable.
