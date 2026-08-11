# incidental-complexity-dominates — Enforcer

## Definition
Configuration, glue, wrappers, lifecycle management, serialization ceremony, or framework rituals occupy more attention than the actual domain problem.

## Trigger When
Configuration, glue, wrappers, lifecycle management, serialization ceremony, or framework rituals occupy more attention than the actual domain problem.

## Do Not Trigger When
Do not fire when the concept is already a named domain type at the boundary, or when an explicit contract already makes the boundary and ownership mechanically visible.

## Distinguish From
pattern-sprawl, premature-optimization, leftover-scaffolding

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
Incidental complexity is dominating the design. Remove ceremony until the essential domain concepts become the visible structure.

## Examples
### Positive
Configuration, glue, wrappers, lifecycle management, serialization ceremony, or framework rituals occupy more attention than the actual domain problem.

### Near miss
Looks related to pattern-sprawl but the decisive signal here is different.

### Counterexample
Do not fire when the concept is already a named domain type at the boundary, or when the suspected smell is only surface similarity.
