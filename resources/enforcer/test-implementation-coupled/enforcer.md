# test-implementation-coupled — Enforcer

## Definition
A test asserts private structure, call counts, helper layout, internal fields, or incidental algorithm choices instead of observable behavior.

## Trigger When
A test asserts private structure, call counts, helper layout, internal fields, or incidental algorithm choices instead of observable behavior.

## Do Not Trigger When
Do not fire when the public contract deliberately includes interaction guarantees (e.g. exactly-once publish) that must be observed through a test double.

## Distinguish From
coverage-theater seeks metrics; weakened-test-to-pass dilutes assertions; this tip locks tests to internals.

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
A test is coupled to implementation details. Assert the public behavior and durable contract instead.

## Examples
### Positive
A test asserts private structure, call counts, helper layout, internal fields, or incidental algorithm choices instead of observable behavior.

### Near miss
A related situation that shares vocabulary but does not cross this tip's boundary — see Distinguish From.

### Counterexample
Do not fire when the public contract deliberately includes interaction guarantees (e.g. exactly-once publish) that must be observed through a test double.
