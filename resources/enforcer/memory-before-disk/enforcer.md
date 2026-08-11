# memory-before-disk — Enforcer

## Definition
Authoritative in-memory state is changed before the durable fact that justifies the change is committed.

## Trigger When
Authoritative in-memory state is changed before the durable fact that justifies the change is committed.

## Do Not Trigger When
Do not fire when the concept is already a named domain type at the boundary, or when an explicit contract already makes the boundary and ownership mechanically visible.

## Distinguish From
log-as-recovery-protocol, partial-write-assumption, overwrite-history

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
Memory was updated before durability. Commit the fact first, then derive runtime state from it.

## Examples
### Positive
Authoritative in-memory state is changed before the durable fact that justifies the change is committed.

### Near miss
Looks related to log-as-recovery-protocol but the decisive signal here is different.

### Counterexample
Do not fire when the concept is already a named domain type at the boundary, or when the suspected smell is only surface similarity.
