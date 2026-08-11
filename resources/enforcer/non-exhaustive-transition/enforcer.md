# non-exhaustive-transition — Enforcer

## Definition
A finite state transition can silently ignore or generically accept a state/event pair that should be explicitly legal or illegal.

## Trigger When
A finite state transition can silently ignore or generically accept a state/event pair that should be explicitly legal or illegal.

## Do Not Trigger When
Do not fire when the concept is already a named domain type at the boundary, or when an explicit contract already makes the boundary and ownership mechanically visible.

## Distinguish From
illegal-state-representable, phase-flag-accumulation, program-counter-state

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
State transitions are not exhaustive. Enumerate the legal cases and reject impossible transitions explicitly.

## Examples
### Positive
A finite state transition can silently ignore or generically accept a state/event pair that should be explicitly legal or illegal.

### Near miss
Looks related to illegal-state-representable but the decisive signal here is different.

### Counterexample
Do not fire when the concept is already a named domain type at the boundary, or when the suspected smell is only surface similarity.
