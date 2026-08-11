# todo-bomb — Enforcer

## Definition
A TODO, FIXME, placeholder, unimplemented branch, or temporary panic defers work required for correctness.

## Trigger When
A TODO, FIXME, placeholder, unimplemented branch, or temporary panic defers work required for correctness.

## Do Not Trigger When
Do not fire for non-blocking backlog notes that do not affect correctness of the shipped path, clearly tracked outside the critical code.

## Distinguish From
spike-not-cleaned promotes experiments; half-finished-refactor leaves migration mid-way; this tip parks required correctness behind TODO/unimplemented.

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
Required correctness has been deferred to a TODO. Finish the behavior or explicitly reject the incomplete change.

## Examples
### Positive
A TODO, FIXME, placeholder, unimplemented branch, or temporary panic defers work required for correctness.

### Near miss
A related situation that shares vocabulary but does not cross this tip's boundary — see Distinguish From.

### Counterexample
Do not fire for non-blocking backlog notes that do not affect correctness of the shipped path, clearly tracked outside the critical code.
