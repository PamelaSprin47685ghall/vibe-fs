# guessed-not-verified — Enforcer

## Definition
Behavior, API shape, file content, Host semantics, or failure cause is asserted without reading the source or running a direct check.

## Trigger When
Behavior, API shape, file content, Host semantics, or failure cause is asserted without reading the source or running a direct check.

## Do Not Trigger When
Do not fire when the concept is already a named domain type at the boundary, or when an explicit contract already makes the boundary and ownership mechanically visible.

## Distinguish From
guess-based-fix, missing-invariant-documentation

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
A material claim was guessed rather than verified. Inspect the authoritative source or run a targeted experiment.

## Examples
### Positive
Behavior, API shape, file content, Host semantics, or failure cause is asserted without reading the source or running a direct check.

### Near miss
Looks related to guess-based-fix but the decisive signal here is different.

### Counterexample
Do not fire when the concept is already a named domain type at the boundary, or when the suspected smell is only surface similarity.
