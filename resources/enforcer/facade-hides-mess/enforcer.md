# facade-hides-mess — Enforcer

## Definition
A new facade or wrapper makes an unhealthy architecture look clean while leaving duplicated ownership or boundary violations underneath.

## Trigger When
A new facade or wrapper makes an unhealthy architecture look clean while leaving duplicated ownership or boundary violations underneath.

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
A facade is concealing unresolved architecture. Repair the underlying ownership and dependency structure.

## Examples
### Positive
A new facade or wrapper makes an unhealthy architecture look clean while leaving duplicated ownership or boundary violations underneath.

### Near miss
A similar surface symptom appears, but the governing boundary already names and enforces the concept.

### Counterexample
A facade is concealing unresolved architecture. Repair the underlying ownership and dependency structure.
