# truncation-skips-damaged — Enforcer

## Definition
Recovery skips corruption in the middle of durable history and continues applying later facts.

## Trigger When
Recovery skips corruption in the middle of durable history and continues applying later facts.

## Do Not Trigger When
Do not fire when only a final incomplete record is truncated by design and interior history remains intact and verified.

## Distinguish From
overwrite-history mutates past facts; recovery-by-filesystem-state sniffs paths; this tip continues past mid-stream corruption.

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
Recovery is continuing past corrupted history. Only a final incomplete record may be truncated; interior corruption must fail closed.

## Examples
### Positive
Recovery skips corruption in the middle of durable history and continues applying later facts.

### Near miss
A related situation that shares vocabulary but does not cross this tip's boundary — see Distinguish From.

### Counterexample
Do not fire when only a final incomplete record is truncated by design and interior history remains intact and verified.
