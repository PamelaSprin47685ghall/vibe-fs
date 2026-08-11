# math-flavored-name — Enforcer

## Definition
Mathematical symbols or abstract single-letter names are used without a real algebraic model and make ordinary domain code harder to read.

## Trigger When
Mathematical symbols or abstract single-letter names are used without a real algebraic model and make ordinary domain code harder to read.

## Do Not Trigger When
Do not fire when the concept is already a named domain type at the boundary, or when an explicit contract already makes the boundary and ownership mechanically visible.

## Distinguish From
misleading-name, abbreviation-anxiety, primitive-obsession

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
Mathematical naming is obscuring an ordinary domain concept. Use names that expose the actual meaning.

## Examples
### Positive
Mathematical symbols or abstract single-letter names are used without a real algebraic model and make ordinary domain code harder to read.

### Near miss
Looks related to misleading-name but the decisive signal here is different.

### Counterexample
Do not fire when the concept is already a named domain type at the boundary, or when the suspected smell is only surface similarity.
