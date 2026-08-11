# scope-creep — Enforcer

## Definition
Implementation expands into unrelated behavior, cleanup, migration, or redesign not required by the current task or governing architecture.

## Trigger When
The implementation expands into unrelated behavior, cleanup, migration, or redesign not required by the current task or governing architecture.

## Do Not Trigger When
Do not fire when adjacent fixes are strictly required for the named acceptance criteria or to keep the tree compiling after a necessary API change.

## Distinguish From
wholesale-rewrite replaces structure broadly; half-finished-refactor leaves mid-migration; this tip is unjustified expansion of intent.

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
The change has expanded beyond its justified scope. Separate unrelated work and keep this delivery focused.

## Examples
### Positive
The implementation expands into unrelated behavior, cleanup, migration, or redesign not required by the current task or governing architecture.

### Near miss
A related situation that shares vocabulary but does not cross this tip's boundary — see Distinguish From.

### Counterexample
Do not fire when adjacent fixes are strictly required for the named acceptance criteria or to keep the tree compiling after a necessary API change.
