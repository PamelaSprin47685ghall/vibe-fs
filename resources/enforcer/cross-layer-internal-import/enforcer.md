# cross-layer-internal-import — Enforcer

## Definition
A higher or unrelated layer imports internal implementation members rather than a declared public boundary.

## Trigger When
A higher or unrelated layer imports internal implementation members rather than a declared public boundary.

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
A layer is reaching through another layer’s boundary. Depend on the public contract, not its internals.

## Examples
### Positive
A higher or unrelated layer imports internal implementation members rather than a declared public boundary.

### Near miss
A similar surface symptom appears, but the governing boundary already names and enforces the concept.

### Counterexample
A layer is reaching through another layer’s boundary. Depend on the public contract, not its internals.
