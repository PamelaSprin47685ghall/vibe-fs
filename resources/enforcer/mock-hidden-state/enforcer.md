# mock-hidden-state — Enforcer

## Definition
A mock changes responses using an invisible cursor, mutable scenario state, request count, or time rather than provider-visible request content.

## Trigger When
A mock changes responses using an invisible cursor, mutable scenario state, request count, or time rather than provider-visible request content.

## Do Not Trigger When
Do not fire when the concept is already a named domain type at the boundary, or when an explicit contract already makes the boundary and ownership mechanically visible.

## Distinguish From
order-dependent-test, impure-core, in-place-mutation

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
The mock depends on hidden state. Make each response a pure function of the visible request.

## Examples
### Positive
A mock changes responses using an invisible cursor, mutable scenario state, request count, or time rather than provider-visible request content.

### Near miss
Looks related to order-dependent-test but the decisive signal here is different.

### Counterexample
Do not fire when the concept is already a named domain type at the boundary, or when the suspected smell is only surface similarity.
