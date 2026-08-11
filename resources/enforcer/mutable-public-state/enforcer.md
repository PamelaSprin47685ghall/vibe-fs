# mutable-public-state — Enforcer

## Definition
State is dangerously public when callers can mutate fields that are supposed to obey invariants, transitions, ownership, or authorization rules.

## Governing Principle
Encapsulation is not secrecy; it is concentration of proof. If every caller may write fields directly, every caller becomes responsible for preserving the object’s invariants and the proof obligation is duplicated across the codebase. Restricting mutation to invariant-preserving operations gives one place authority to decide which transitions are legal.

## Trigger When
Trigger when externally reachable code can assign domain state directly, especially when setters/fields can bypass validation, event emission, authorization, or state-transition rules.

## Do Not Trigger When
Do not trigger for immutable public data or intentionally mutable low-level data structures whose entire contract is unrestricted mutation and which carry no higher invariant.

## Distinguish From
in-place-mutation concerns destructive update itself. illegal-state-representable concerns the set of possible values. This rule concerns who is authorized to perform state change.

## Decision Procedure
List the invariants every write must preserve. If callers can modify relevant fields without passing through code that enforces those invariants, move write authority behind invariant-preserving operations.

## Nudge
Put the proof where the state changes. Expose observations freely, but let only named operations that preserve invariants create new authoritative state.
