# shared-mutable-concurrency — Enforcer

## Definition
Concurrent workers coordinate by mutating shared state under ad hoc locks instead of ownership or message passing.

## Trigger When
Concurrent workers coordinate by mutating shared state protected by ad hoc locks rather than owning state or exchanging messages.

## Do Not Trigger When
Do not fire when a single-threaded owner or actor model already serializes mutation, or when a well-bounded concurrent structure is the documented design.

## Distinguish From
in-place-mutation is general mutability; race-first-wins-semantics is about completion order as truth; this tip is shared mutable coordination.

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
Concurrent workers share mutable state. Prefer ownership, message passing, or a single serialized writer.

## Examples
### Positive
Concurrent workers coordinate by mutating shared state protected by ad hoc locks rather than owning state or exchanging messages.

### Near miss
A related situation that shares vocabulary but does not cross this tip's boundary — see Distinguish From.

### Counterexample
Do not fire when a single-threaded owner or actor model already serializes mutation, or when a well-bounded concurrent structure is the documented design.
