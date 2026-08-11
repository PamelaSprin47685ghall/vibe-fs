# unverified-completion-claim — Enforcer

## Definition
Work is declared complete without running the relevant tests, checks, build, reproduction, or observable verification.

## Trigger When
Work is declared complete without running the relevant tests, checks, build, reproduction, or observable verification.

## Do Not Trigger When
Do not fire when appropriate verification was run and evidence is reported, or when the task is pure planning with no behavioral deliverable.

## Distinguish From
false-gate fakes green; tool-error-ignored continues past red; release-ladder-skipped skips ordered rungs; this tip claims done without verification.

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
Completion was claimed without verification. Run the relevant behavioral checks and report the actual result.

## Examples
### Positive
Work is declared complete without running the relevant tests, checks, build, reproduction, or observable verification.

### Near miss
A related situation that shares vocabulary but does not cross this tip's boundary — see Distinguish From.

### Counterexample
Do not fire when appropriate verification was run and evidence is reported, or when the task is pure planning with no behavioral deliverable.
