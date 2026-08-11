# misleading-name — Enforcer

## Definition
A name suggests stronger guarantees, different ownership, broader scope, or a different domain meaning than the implementation provides.

## Trigger When
A name suggests stronger guarantees, different ownership, broader scope, or a different domain meaning than the implementation provides.

## Do Not Trigger When
Do not fire when the concept is already a named domain type at the boundary, or when an explicit contract already makes the boundary and ownership mechanically visible.

## Distinguish From
math-flavored-name, primitive-obsession, illegal-state-representable

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
A name misrepresents the concept or guarantee. Rename it to match the actual domain fact.

## Examples
### Positive
A name suggests stronger guarantees, different ownership, broader scope, or a different domain meaning than the implementation provides.

### Near miss
Looks related to math-flavored-name but the decisive signal here is different.

### Counterexample
Do not fire when the concept is already a named domain type at the boundary, or when the suspected smell is only surface similarity.
