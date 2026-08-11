# implicit-convention-magic — Enforcer

## Definition
Correctness depends on file names, registration order, reflection, annotations, directory placement, or framework discovery that is not mechanically visible at the call site.

## Trigger When
Correctness depends on file names, registration order, reflection, annotations, directory placement, or framework discovery that is not mechanically visible at the call site.

## Do Not Trigger When
Do not fire when the concept is already a named domain type at the boundary, or when an explicit contract already makes the boundary and ownership mechanically visible.

## Distinguish From
implicit-control-flow, missing-architecture-gate

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
Correctness depends on hidden convention. Replace it with an explicit typed registration or contract.

## Examples
### Positive
Correctness depends on file names, registration order, reflection, annotations, directory placement, or framework discovery that is not mechanically visible at the call site.

### Near miss
Looks related to implicit-control-flow but the decisive signal here is different.

### Counterexample
Do not fire when the concept is already a named domain type at the boundary, or when the suspected smell is only surface similarity.
