# log-as-recovery-protocol — Enforcer

## Definition
Diagnostic logs, log messages, or log ordering are used to decide what durable business work occurred.

## Trigger When
Diagnostic logs, log messages, or log ordering are used to decide what durable business work occurred.

## Do Not Trigger When
Do not fire when the concept is already a named domain type at the boundary, or when an explicit contract already makes the boundary and ownership mechanically visible.

## Distinguish From
memory-before-disk, partial-write-assumption, overwrite-history

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
Diagnostic logs are being used as recovery facts. Recover from the journal and authoritative external state instead.

## Examples
### Positive
Diagnostic logs, log messages, or log ordering are used to decide what durable business work occurred.

### Near miss
Looks related to memory-before-disk but the decisive signal here is different.

### Counterexample
Do not fire when the concept is already a named domain type at the boundary, or when the suspected smell is only surface similarity.
