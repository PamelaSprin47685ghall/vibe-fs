# primitive-obsession — Enforcer

## Definition
A domain concept such as an account ID, order ID, money amount, path, digest, or capability crosses a meaningful boundary as an undifferentiated string, number, or boolean.

## Trigger When
A domain concept such as an account ID, order ID, money amount, path, digest, or capability crosses a meaningful boundary as an undifferentiated string, number, or boolean.

## Do Not Trigger When
Do not fire when the concept is already a named domain type at the boundary, or when an explicit contract already makes the boundary and ownership mechanically visible.

## Distinguish From
null-ambiguity, illegal-state-representable, misleading-name

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
A domain concept is crossing a boundary as a primitive. Introduce a distinct type so invalid substitutions become impossible.

## Examples
### Positive
A domain concept such as an account ID, order ID, money amount, path, digest, or capability crosses a meaningful boundary as an undifferentiated string, number, or boolean.

### Near miss
Looks related to null-ambiguity but the decisive signal here is different.

### Counterexample
Do not fire when the concept is already a named domain type at the boundary, or when the suspected smell is only surface similarity.
