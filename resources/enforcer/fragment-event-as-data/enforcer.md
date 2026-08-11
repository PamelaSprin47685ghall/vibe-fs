# fragment-event-as-data — Enforcer

## Definition
Partial stream events, deltas, update ordering, or transport fragments are assembled into business facts instead of reading a complete snapshot.

## Trigger When
Partial stream events, deltas, update ordering, or transport fragments are assembled into business facts instead of reading a complete snapshot.

## Do Not Trigger When
Do not fire when the concept is already a named domain type at the boundary, or when the observed pattern is intentional, documented, and verified at the owning contract.

## Distinguish From
Related tips that share vocabulary but different boundary.

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
Transport fragments are being treated as domain data. Use events only as wake-up signals and read the complete authoritative snapshot.

## Examples
### Positive
Partial stream events, deltas, update ordering, or transport fragments are assembled into business facts instead of reading a complete snapshot.

### Near miss
A similar surface symptom appears, but the governing boundary already names and enforces the concept.

### Counterexample
Transport fragments are being treated as domain data. Use events only as wake-up signals and read the complete authoritative snapshot.
