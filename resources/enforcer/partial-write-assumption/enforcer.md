# partial-write-assumption — Enforcer

## Definition
Recovery logic assumes an append or effect may be partially committed despite the storage contract defining committed versus unknown outcomes.

## Trigger When
Recovery logic assumes an append or effect may be partially committed despite the storage contract defining committed versus unknown outcomes.

## Do Not Trigger When
Do not fire when the concept is already a named domain type at the boundary, or when an explicit contract already makes the boundary and ownership mechanically visible.

## Distinguish From
memory-before-disk, guessed-migration, optimistic-retry-assumption

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
Recovery is inventing a partial-write state. Follow the storage contract’s explicit committed and unknown outcomes.

## Examples
### Positive
Recovery logic assumes an append or effect may be partially committed despite the storage contract defining committed versus unknown outcomes.

### Near miss
Looks related to memory-before-disk but the decisive signal here is different.

### Counterexample
Do not fire when the concept is already a named domain type at the boundary, or when the suspected smell is only surface similarity.
