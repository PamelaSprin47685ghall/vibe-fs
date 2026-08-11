# guessed-migration — Enforcer

## Definition
Old durable data is heuristically interpreted or silently upgraded without a specified migration.

## Trigger When
Old durable data is heuristically interpreted or silently upgraded without a specified migration.

## Do Not Trigger When
Do not fire when the concept is already a named domain type at the boundary, or when an explicit contract already makes the boundary and ownership mechanically visible.

## Distinguish From
partial-write-assumption, memory-before-disk, overwrite-history

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
An old schema is being guessed into a new one. Use an explicit migration or fail closed.

## Examples
### Positive
Old durable data is heuristically interpreted or silently upgraded without a specified migration.

### Near miss
Looks related to partial-write-assumption but the decisive signal here is different.

### Counterexample
Do not fire when the concept is already a named domain type at the boundary, or when the suspected smell is only surface similarity.
