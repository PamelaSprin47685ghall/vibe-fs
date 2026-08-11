# timeout-inflated-to-pass — Enforcer

## Definition
A timeout or retry budget is increased mainly to turn a failing test or hanging operation green.

## Trigger When
A timeout or retry budget is increased mainly to turn a failing test or hanging operation green.

## Do Not Trigger When
Do not fire when a timeout is raised from measured capacity planning with a defined SLO and root-cause analysis, not to silence a hang.

## Distinguish From
repeat-until-pass loops runs; sleep-based-synchronization uses fixed delay; this tip grows budgets to hide failure.

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
A larger timeout is masking the failure. Fix the missing causal signal or resource leak instead.

## Examples
### Positive
A timeout or retry budget is increased mainly to turn a failing test or hanging operation green.

### Near miss
A related situation that shares vocabulary but does not cross this tip's boundary — see Distinguish From.

### Counterexample
Do not fire when a timeout is raised from measured capacity planning with a defined SLO and root-cause analysis, not to silence a hang.
