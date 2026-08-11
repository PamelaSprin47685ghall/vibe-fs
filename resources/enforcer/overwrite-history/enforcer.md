# overwrite-history — Enforcer

## Definition
Previously committed facts are edited or deleted to represent correction instead of appending a compensating or superseding fact.

## Trigger When
Previously committed facts are edited or deleted to represent correction instead of appending a compensating or superseding fact.

## Do Not Trigger When
Do not fire when the concept is already a named domain type at the boundary, or when an explicit contract already makes the boundary and ownership mechanically visible.

## Distinguish From
memory-before-disk, in-place-mutation, log-as-recovery-protocol

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
History is being rewritten. Preserve the original fact and append an explicit correction or replacement.

## Examples
### Positive
Previously committed facts are edited or deleted to represent correction instead of appending a compensating or superseding fact.

### Near miss
Looks related to memory-before-disk but the decisive signal here is different.

### Counterexample
Do not fire when the concept is already a named domain type at the boundary, or when the suspected smell is only surface similarity.
