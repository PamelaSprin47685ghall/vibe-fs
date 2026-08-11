# half-finished-refactor — Enforcer

## Definition
Old and new structures coexist without a completed migration, leaving duplicated ownership, temporary adapters, or inconsistent conventions.

## Trigger When
Old and new structures coexist without a completed migration, leaving duplicated ownership, temporary adapters, or inconsistent conventions.

## Do Not Trigger When
Do not fire when the concept is already a named domain type at the boundary, or when an explicit contract already makes the boundary and ownership mechanically visible.

## Distinguish From
legacy-cruft-retained, leftover-scaffolding, premature-unification

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
A refactor stopped halfway. Finish the ownership transfer and remove the obsolete path.

## Examples
### Positive
Old and new structures coexist without a completed migration, leaving duplicated ownership, temporary adapters, or inconsistent conventions.

### Near miss
Looks related to legacy-cruft-retained but the decisive signal here is different.

### Counterexample
Do not fire when the concept is already a named domain type at the boundary, or when the suspected smell is only surface similarity.
