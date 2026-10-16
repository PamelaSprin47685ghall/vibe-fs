# mutable-public-state — Lesson

## Opinion

### Definition

State is dangerously public when callers can mutate fields that are supposed to obey invariants, transitions, ownership, or authorization rules. The root-cause is that write authority is distributed to callers while the proof obligation for those invariants remains concentrated in the type's intended transitions.

### Governing Principle

Encapsulation is not secrecy; it is concentration of proof. If every caller may write fields directly, every caller becomes responsible for preserving the object's invariants and the proof obligation is duplicated across the codebase. Restricting mutation to invariant-preserving operations gives one place authority to decide which transitions are legal.

### Trigger When

Trigger when externally reachable code can assign domain state directly, especially when setters/fields can bypass validation, event emission, authorization, or state-transition rules.

### Do Not Trigger When

- The data is immutable once published, or is an intentionally mutable low-level structure whose entire contract is unrestricted mutation and which carries no higher invariant.
- Mutation is internal to the owning module and not reachable from callers.
- The type is a DTO at an adapter edge with no domain invariant of its own.
- The field is a published observation (read-only view, copy, or snapshot) and callers cannot write the authoritative state.

### Distinguish From

`in-place-mutation` concerns destructive update itself. `illegal-state-representable` concerns the set of possible values. Tie-break: if callers are authorized to write fields that carry invariants, this rule; if the problem is destructive update even behind a method, `in-place-mutation`; if invalid combinations are constructible even without public writes, `illegal-state-representable`.

### Decision Procedure

List the invariants every write must preserve. If callers can modify relevant fields without passing through code that enforces those invariants, move write authority behind invariant-preserving operations.

### Examples

- positive: Order status is a public field; callers set `Cancelled` without running cancellation rules.
- near-miss: A pixel buffer exposes mutable bytes because unrestricted mutation is the whole contract.
- counterexample: Status changes only through `cancel()` / `ship()` which validate and emit events.

## Advice

### What To Do Now

Remove public write access to invariant-bearing fields and expose domain operations that produce valid next state instead. The named transition that preserves the object's rules is who owns every authoritative write; callers observe, they do not assign.

### Why This Matters

Public mutation distributes authority without distributing complete knowledge. Each caller can create a state the object itself would never choose, then every downstream consumer must defend against those possibilities. Encapsulation pays the reasoning cost once at the transition boundary.

### Repair Strategy

Keep state immutable or privately owned, define named operations for legitimate transitions, and make each operation return the new value or typed failure. Avoid generic setters when fields have domain meaning.

### Decision Branches

- If callers can assign invariant-bearing fields, hide writes and expose named transitions that enforce the rules.
- If the structure's contract is unrestricted mutation with no higher invariant, leave it public and do not invent false encapsulation.

### Common Wrong Fixes

- Hide fields behind setters that accept any value and perform no domain decision.
- Make fields private but expose a generic `update(patch)` that bypasses transitions.
- Copy validation into every caller instead of concentrating it at the write boundary.

### Verification

Attempt the formerly invalid direct update from a caller. It should be impossible without invoking the operation that owns and verifies the transition. The invariant is that every authoritative write preserves the object's rules.

### Done When

All authoritative state changes pass through a small set of named invariant-preserving operations, and callers cannot bypass the domain's rules by assignment.

### Nudge

Put the proof where the state changes. Expose observations freely, but let only named operations that preserve invariants create new authoritative state.
