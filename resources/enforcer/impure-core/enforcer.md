# impure-core — Enforcer

## Definition
Core business decisions directly read clocks, random sources, databases, networks, environment state, or mutable globals.

## Trigger When
Core business decisions directly read clocks, random sources, databases, networks, environment state, or mutable globals.

## Do Not Trigger When
Do not fire when the concept is already a named domain type at the boundary, or when an explicit contract already makes the boundary and ownership mechanically visible.

## Distinguish From
mixed-side-effect-boundaries, in-place-mutation, mutable-public-state

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
Business policy is entangled with effects. Move effects to the shell and pass explicit values into a pure core.

## Examples
### Positive
Core business decisions directly read clocks, random sources, databases, networks, environment state, or mutable globals.

### Near miss
Looks related to mixed-side-effect-boundaries but the decisive signal here is different.

### Counterexample
Do not fire when the concept is already a named domain type at the boundary, or when the suspected smell is only surface similarity.
