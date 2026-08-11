# null-ambiguity — Enforcer

## Definition
`null`, missing, empty, or optional values conflate different outcomes such as absent, unauthorized, failed, not loaded, or not applicable.

## Trigger When
`null`, missing, empty, or optional values conflate different outcomes such as absent, unauthorized, failed, not loaded, or not applicable.

## Do Not Trigger When
Do not fire when the concept is already a named domain type at the boundary, or when an explicit contract already makes the boundary and ownership mechanically visible.

## Distinguish From
illegal-state-representable, primitive-obsession, misleading-name

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
A nullable value is carrying several meanings. Model the outcomes as explicit alternatives.

## Examples
### Positive
`null`, missing, empty, or optional values conflate different outcomes such as absent, unauthorized, failed, not loaded, or not applicable.

### Near miss
Looks related to illegal-state-representable but the decisive signal here is different.

### Counterexample
Do not fire when the concept is already a named domain type at the boundary, or when the suspected smell is only surface similarity.
