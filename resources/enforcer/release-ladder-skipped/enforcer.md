# release-ladder-skipped — Enforcer

## Definition
Validation jumps to a high-level test or release without clearing required lower pure, contract, replay, and canary gates.

## Trigger When
Validation jumps directly to a high-level test or release without passing the required lower-level pure, contract, replay, and canary stages.

## Do Not Trigger When
Do not fire when the change is docs-only or pure content with no behavioral surface, or when the ladder stages that apply are already green.

## Distinguish From
unverified-completion-claim skips verification entirely; canary-skipped is one rung; this tip is skipping the ordered ladder.

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
The verification ladder was skipped. Pass each lower-level gate before promoting the change.

## Examples
### Positive
Validation jumps directly to a high-level test or release without passing the required lower-level pure, contract, replay, and canary stages.

### Near miss
A related situation that shares vocabulary but does not cross this tip's boundary — see Distinguish From.

### Counterexample
Do not fire when the change is docs-only or pure content with no behavioral surface, or when the ladder stages that apply are already green.
