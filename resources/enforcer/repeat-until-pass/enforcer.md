# repeat-until-pass — Enforcer

## Definition
A test or command is rerun until it happens to succeed, and that lucky success is treated as verification.

## Trigger When
A test or command is rerun until it happens to succeed, and the successful repetition is treated as verification.

## Do Not Trigger When
Do not fire when a single deterministic retry policy exists for known transient infrastructure faults outside the system under test, with failure still reported after budget.

## Distinguish From
flaky-test-tolerated leaves flakes known; timeout-inflated-to-pass masks hangs; this tip treats reruns as the fix.

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
Repetition is hiding a nondeterministic failure. Make one run deterministic instead of retrying until green.

## Examples
### Positive
A test or command is rerun until it happens to succeed, and the successful repetition is treated as verification.

### Near miss
A related situation that shares vocabulary but does not cross this tip's boundary — see Distinguish From.

### Counterexample
Do not fire when a single deterministic retry policy exists for known transient infrastructure faults outside the system under test, with failure still reported after budget.
