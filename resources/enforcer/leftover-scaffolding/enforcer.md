# leftover-scaffolding — Enforcer

## Definition
Temporary files, experimental branches, probes, fixtures, flags, scripts, or migration scaffolding remain in the delivered result without a permanent role.

## Trigger When
Temporary files, experimental branches, probes, fixtures, flags, scripts, or migration scaffolding remain in the delivered result without a permanent role.

## Do Not Trigger When
Do not fire when the concept is already a named domain type at the boundary, or when an explicit contract already makes the boundary and ownership mechanically visible.

## Distinguish From
legacy-cruft-retained, half-finished-refactor, manual-toil-repeat

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
Temporary scaffolding remains in the delivery. Remove it or promote it into a maintained tool with a clear owner.

## Examples
### Positive
Temporary files, experimental branches, probes, fixtures, flags, scripts, or migration scaffolding remain in the delivered result without a permanent role.

### Near miss
Looks related to legacy-cruft-retained but the decisive signal here is different.

### Counterexample
Do not fire when the concept is already a named domain type at the boundary, or when the suspected smell is only surface similarity.
