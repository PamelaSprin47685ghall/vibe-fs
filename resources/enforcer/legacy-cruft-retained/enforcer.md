# legacy-cruft-retained — Enforcer

## Definition
Obsolete code, aliases, compatibility branches, or old names are kept despite an explicit clean-break policy.

## Trigger When
Obsolete code, aliases, compatibility branches, or old names are kept despite an explicit clean-break policy.

## Do Not Trigger When
Do not fire when the concept is already a named domain type at the boundary, or when an explicit contract already makes the boundary and ownership mechanically visible.

## Distinguish From
leftover-scaffolding, half-finished-refactor, misleading-name

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
Obsolete compatibility code is being retained. Complete the clean break and remove the old surface.

## Examples
### Positive
Obsolete code, aliases, compatibility branches, or old names are kept despite an explicit clean-break policy.

### Near miss
Looks related to leftover-scaffolding but the decisive signal here is different.

### Counterexample
Do not fire when the concept is already a named domain type at the boundary, or when the suspected smell is only surface similarity.
