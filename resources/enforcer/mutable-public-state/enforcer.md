# mutable-public-state — Enforcer

## Definition
State is dangerously public when callers can mutate fields that are supposed to obey invariants, transitions, ownership, or authorization rules. The root-cause is that write authority is distributed to callers while the proof obligation for those invariants remains concentrated in the type's intended transitions.

## Governing Principle
Encapsulation is not secrecy; it is concentration of proof. If every caller may write fields directly, every caller becomes responsible for preserving the object's invariants and the proof obligation is duplicated across the codebase. Restricting mutation to invariant-preserving operations gives one place authority to decide which transitions are legal.

## Trigger When
Trigger when externally reachable code can assign domain state directly, especially when setters/fields can bypass validation, event emission, authorization, or state-transition rules.

## Do Not Trigger When
- The data is immutable once published, or is an intentionally mutable low-level structure whose entire contract is unrestricted mutation and which carries no higher invariant.
- Mutation is internal to the owning module and not reachable from callers.
- The type is a DTO at an adapter edge with no domain invariant of its own.
- The field is a published observation (read-only view, copy, or snapshot) and callers cannot write the authoritative state.

## Distinguish From
`in-place-mutation` concerns destructive update itself. `illegal-state-representable` concerns the set of possible values. Tie-break: if callers are authorized to write fields that carry invariants, this rule; if the problem is destructive update even behind a method, `in-place-mutation`; if invalid combinations are constructible even without public writes, `illegal-state-representable`.

## Decision Procedure
List the invariants every write must preserve. If callers can modify relevant fields without passing through code that enforces those invariants, move write authority behind invariant-preserving operations.

## Examples
- positive: Order status is a public field; callers set `Cancelled` without running cancellation rules.
- near-miss: A pixel buffer exposes mutable bytes because unrestricted mutation is the whole contract.
- counterexample: Status changes only through `cancel()` / `ship()` which validate and emit events.

## Nudge
Put the proof where the state changes. Expose observations freely, but let only named operations that preserve invariants create new authoritative state.
