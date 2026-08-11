# time-dependent-test — Enforcer

## Definition
A test depends on real current time, wall-clock delays, time zones, or timing luck.

## Trigger When
A test depends on real current time, wall-clock delays, time zones, or timing luck.

## Do Not Trigger When
Do not fire when the suite intentionally exercises the real clock in a narrow integration smoke with stable tolerances and no flake history.

## Distinguish From
time-source-in-logic is production code reading the clock; sleep-based-synchronization uses sleep for causality; this tip is tests coupled to real time.

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
A test depends on real time. Inject time and make the scenario deterministic.

## Examples
### Positive
A test depends on real current time, wall-clock delays, time zones, or timing luck.

### Near miss
A related situation that shares vocabulary but does not cross this tip's boundary — see Distinguish From.

### Counterexample
Do not fire when the suite intentionally exercises the real clock in a narrow integration smoke with stable tolerances and no flake history.
