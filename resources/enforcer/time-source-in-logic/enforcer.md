# time-source-in-logic — Enforcer

## Definition
Domain logic reads the current clock internally instead of receiving an explicit time value or clock port.

## Trigger When
Domain logic reads the current clock internally instead of receiving an explicit time value or clock port.

## Do Not Trigger When
Do not fire when only adapters/IO edges read the clock and pass an instant into pure domain functions.

## Distinguish From
random-source-in-logic hides entropy; time-dependent-test is the test symptom; this tip is hidden clocks in domain policy.

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
Time is an implicit dependency. Inject the relevant instant or clock so behavior is deterministic and testable.

## Examples
### Positive
Domain logic reads the current clock internally instead of receiving an explicit time value or clock port.

### Near miss
A related situation that shares vocabulary but does not cross this tip's boundary — see Distinguish From.

### Counterexample
Do not fire when only adapters/IO edges read the clock and pass an instant into pure domain functions.
