# race-first-wins-semantics — Enforcer

## Definition
Business meaning is decided by scheduler order or whichever concurrent call finishes first, even when the calls carry different information.

## Trigger When
Scheduling order or the first completing concurrent call determines a domain result even though the calls may carry different information.

## Do Not Trigger When
Do not fire when a documented first-writer-wins or quorum rule is an explicit domain decision with stable identity and merge policy.

## Distinguish From
shared-mutable-concurrency is about lock-protected shared mutation; unbounded-fanout is about missing concurrency bounds; this tip is about race order becoming domain truth.

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
A scheduler race is deciding business semantics. Collect the complete set and merge it deterministically.

## Examples
### Positive
Scheduling order or the first completing concurrent call determines a domain result even though the calls may carry different information.

### Near miss
A related situation that shares vocabulary but does not cross this tip's boundary — see Distinguish From.

### Counterexample
Do not fire when a documented first-writer-wins or quorum rule is an explicit domain decision with stable identity and merge policy.
