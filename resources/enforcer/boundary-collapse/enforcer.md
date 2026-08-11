# boundary-collapse — Enforcer

## Definition
Modules with different invariants or lifecycles directly share internals, mutate each other’s state, or bypass explicit translation at the boundary.

## Trigger When
Modules with different invariants or lifecycles directly share internals, mutate each other’s state, or bypass explicit translation at the boundary.

## Do Not Trigger When
Do not fire when the concept is already a named domain type at the boundary, or when the observed pattern is intentional, documented, and verified at the owning contract.

## Distinguish From
Related tips that share vocabulary but different boundary.

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
A context boundary has collapsed. Restore a clear interface and pass only the facts that genuinely cross it.

## Examples
### Positive
Modules with different invariants or lifecycles directly share internals, mutate each other’s state, or bypass explicit translation at the boundary.

### Near miss
A similar surface symptom appears, but the governing boundary already names and enforces the concept.

### Counterexample
A context boundary has collapsed. Restore a clear interface and pass only the facts that genuinely cross it.
