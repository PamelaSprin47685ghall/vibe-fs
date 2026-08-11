# god-module — Enforcer

## Definition
One module owns several unrelated side-effect boundaries, policies, resources, or domains merely because they are currently convenient to colocate.

## Trigger When
One module owns several unrelated side-effect boundaries, policies, resources, or domains merely because they are currently convenient to colocate.

## Do Not Trigger When
Do not fire when the concept is already a named domain type at the boundary, or when an explicit contract already makes the boundary and ownership mechanically visible.

## Distinguish From
mixed-side-effect-boundaries, premature-unification, pattern-sprawl

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
One module owns unrelated responsibilities. Split it along real domain or side-effect boundaries, not arbitrary file size.

## Examples
### Positive
One module owns several unrelated side-effect boundaries, policies, resources, or domains merely because they are currently convenient to colocate.

### Near miss
Looks related to mixed-side-effect-boundaries but the decisive signal here is different.

### Counterexample
Do not fire when the concept is already a named domain type at the boundary, or when the suspected smell is only surface similarity.
