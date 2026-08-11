# wholesale-rewrite — Enforcer

## Definition
A broad rewrite, generated replacement, or large delete-and-recreate is chosen instead of a precise change that preserves known-good structure.

## Trigger When
A broad rewrite, generated replacement, or large delete-and-recreate operation is chosen instead of a precise change preserving known-good structure.

## Do Not Trigger When
Do not fire when the task is an authorized greenfield replacement with explicit scope, or when the existing structure is irredeemably wrong and a rewrite is the decided path.

## Distinguish From
scope-creep expands intent; half-finished-refactor leaves migration incomplete; this tip prefers blast-radius rewrites over precise repair.

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
A wholesale rewrite is replacing a targeted repair. Make the smallest structurally correct change.

## Examples
### Positive
A broad rewrite, generated replacement, or large delete-and-recreate operation is chosen instead of a precise change preserving known-good structure.

### Near miss
A related situation that shares vocabulary but does not cross this tip's boundary — see Distinguish From.

### Counterexample
Do not fire when the task is an authorized greenfield replacement with explicit scope, or when the existing structure is irredeemably wrong and a rewrite is the decided path.
