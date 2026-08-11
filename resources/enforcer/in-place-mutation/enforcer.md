# in-place-mutation — Enforcer

## Definition
In-place mutation overwrites a shared or externally visible value so the transition from old state to new state exists only as a moment in execution, not as an explicit value or fact.

## Governing Principle
State change contains information: both that A existed and that the system moved from A to B. Overwriting A with B destroys half of that information and makes readers reconstruct transitions from control flow. Immutable transformation or explicit events preserve the relation itself, enabling replay, comparison, audit, concurrency reasoning, and tests that speak about before/after values without depending on hidden identity.

## Trigger When
Trigger when shared/exposed domain state is mutated field-by-field or overwritten in place and correctness depends on observers seeing a coherent transition.

## Do Not Trigger When
- Do not trigger for purely local mutation inside a function whose mutable cell cannot escape, carries no identity, and is only an efficient implementation of a pure result.
- Do not trigger for builder or accumulator objects that never escape the constructing function.
- Do not trigger for hardware/FFI buffers whose API is inherently mutating when domain state still transitions as values at the wrapper boundary.

## Distinguish From
mutable-public-state exposes mutation authority to callers. overwrite-history mutates durable past facts. This rule concerns destructive update of current shared state. Tie-break: if callers can mutate the object, use mutable-public-state; if a shared current value is overwritten in place, use this rule.

## Decision Procedure
Ask whether any other component can observe, retain, race with, or reason about the old value. If yes, the root-cause is destruction of the transition at that shared identity: compute a new value or record an explicit transition instead of mutating it. Prefer this over downstream locks, logs, or snapshots that merely reconstruct what overwrite destroyed.

## Examples
- positive: A shared domain record is updated field-by-field while other components hold the same reference.
- near-miss: A local buffer is mutated inside a function and never escapes; the function returns a new value.
- counterexample: Next state is computed as a new value or event; the authoritative reference swaps after the transition is complete.

## Nudge
A transition is information. Preserve it by producing a new value or event; use mutation only as a local unobservable implementation detail.
