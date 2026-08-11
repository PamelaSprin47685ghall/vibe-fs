# implicit-control-flow — Enforcer

## Definition
Critical ordering depends on callbacks, registration order, hidden lifecycle hooks, global initialization, or undocumented framework conventions.

## Trigger When
Critical ordering depends on callbacks, registration order, hidden lifecycle hooks, global initialization, or undocumented framework conventions.

## Do Not Trigger When
Do not fire when the concept is already a named domain type at the boundary, or when an explicit contract already makes the boundary and ownership mechanically visible.

## Distinguish From
implicit-convention-magic, program-counter-state, phase-flag-accumulation

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
Essential control flow is implicit. Make the ordering and ownership explicit in ordinary program structure.

## Examples
### Positive
Critical ordering depends on callbacks, registration order, hidden lifecycle hooks, global initialization, or undocumented framework conventions.

### Near miss
Looks related to implicit-convention-magic but the decisive signal here is different.

### Counterexample
Do not fire when the concept is already a named domain type at the boundary, or when the suspected smell is only surface similarity.
