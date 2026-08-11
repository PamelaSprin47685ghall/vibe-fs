# tool-error-ignored — Enforcer

## Definition
A tool, command, test, patch, search, or process reports an error that is skipped or treated as irrelevant without resolution.

## Trigger When
A tool, command, test, patch, search, or process reports an error that is skipped or treated as irrelevant without resolution.

## Do Not Trigger When
Do not fire when the error is explicitly classified as non-blocking with a recorded reason and alternative evidence covers the goal.

## Distinguish From
unverified-completion-claim skips checks entirely; false-gate pretends green; this tip ignores a raised error and continues.

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
A tool error was ignored. Resolve or explicitly account for it before proceeding.

## Examples
### Positive
A tool, command, test, patch, search, or process reports an error that is skipped or treated as irrelevant without resolution.

### Near miss
A related situation that shares vocabulary but does not cross this tip's boundary — see Distinguish From.

### Counterexample
Do not fire when the error is explicitly classified as non-blocking with a recorded reason and alternative evidence covers the goal.
