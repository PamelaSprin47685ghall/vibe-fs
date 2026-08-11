# duplicated-control-flow — Enforcer

## Definition
The same workflow, retry sequence, validation order, or state transition algorithm is independently implemented in multiple places.

## Trigger When
The same workflow, retry sequence, validation order, or state transition algorithm is independently implemented in multiple places.

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
The same control algorithm has multiple owners. Establish one canonical implementation and route all callers through it.

## Examples
### Positive
The same workflow, retry sequence, validation order, or state transition algorithm is independently implemented in multiple places.

### Near miss
A similar surface symptom appears, but the governing boundary already names and enforces the concept.

### Counterexample
The same control algorithm has multiple owners. Establish one canonical implementation and route all callers through it.
