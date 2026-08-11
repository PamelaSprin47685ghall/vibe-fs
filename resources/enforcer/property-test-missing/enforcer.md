# property-test-missing — Enforcer

## Definition
A general invariant over a parser, serializer, fold, transition, merge, normalization, or algebraic operation is covered only by a few hand-picked examples.

## Trigger When
A parser, serializer, fold, state transition, merge, normalization, round trip, or algebraic operation is tested only with a few examples despite clear general invariants.

## Do Not Trigger When
Do not fire when the operation is a one-off glue path with no stable algebraic invariant, or when property tests already cover the invariant space.

## Distinguish From
weakened-test-to-pass removes assertions to go green; failure-path-untested omits negative cases; this tip is about missing generative coverage of a known general law.

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
A general invariant is covered only by examples. Add property-based tests for the full input space.

## Examples
### Positive
A parser, serializer, fold, state transition, merge, normalization, round trip, or algebraic operation is tested only with a few examples despite clear general invariants.

### Near miss
A related situation that shares vocabulary but does not cross this tip's boundary — see Distinguish From.

### Counterexample
Do not fire when the operation is a one-off glue path with no stable algebraic invariant, or when property tests already cover the invariant space.
