# stale-documentation — Enforcer

## Definition
Code or behavior changes while authoritative documentation, schemas, examples, or diagrams still describe the old contract.

## Trigger When
Code or behavior changes while authoritative documentation, schemas, examples, or diagrams continue to describe the old contract.

## Do Not Trigger When
Do not fire for informal notes clearly marked non-authoritative, or for docs outside the owning change when another process updates them atomically the same release.

## Distinguish From
comment-theater adds non-owning commentary; misleading-name is identifier drift; this tip is authoritative docs out of sync.

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
The implementation and authoritative documentation disagree. Update the owning specification in the same change.

## Examples
### Positive
Code or behavior changes while authoritative documentation, schemas, examples, or diagrams continue to describe the old contract.

### Near miss
A related situation that shares vocabulary but does not cross this tip's boundary — see Distinguish From.

### Counterexample
Do not fire for informal notes clearly marked non-authoritative, or for docs outside the owning change when another process updates them atomically the same release.
