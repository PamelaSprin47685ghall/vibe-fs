# in-place-mutation — Enforcer

## Definition
In-place mutation overwrites a shared or externally visible value so the transition from old state to new state exists only as a moment in execution, not as an explicit value or fact.

## Governing Principle
State change contains information: both that A existed and that the system moved from A to B. Overwriting A with B destroys half of that information and makes readers reconstruct transitions from control flow. Immutable transformation or explicit events preserve the relation itself, enabling replay, comparison, audit, concurrency reasoning, and tests that speak about before/after values without depending on hidden identity.

## Trigger When
Trigger when shared/exposed domain state is mutated field-by-field or overwritten in place and correctness depends on observers seeing a coherent transition.

## Do Not Trigger When
Do not trigger for purely local mutation inside a function whose mutable cell cannot escape, carries no identity, and is only an efficient implementation of a pure result.

## Distinguish From
mutable-public-state exposes mutation authority to callers. overwrite-history mutates durable past facts. This rule concerns destructive update of current shared state.

## Decision Procedure
Ask whether any other component can observe, retain, race with, or reason about the old value. If yes, compute a new value or record an explicit transition instead of mutating shared identity.

## Nudge
A transition is information. Preserve it by producing a new value or event; use mutation only as a local unobservable implementation detail.
