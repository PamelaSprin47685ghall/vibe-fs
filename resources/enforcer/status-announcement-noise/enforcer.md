# status-announcement-noise — Enforcer

## Definition
Production output, comments, logs, or agent messages repeatedly announce routine progress without a decision, result, failure, or required action.

## Trigger When
Production output, code comments, logs, or agent messages repeatedly announce routine progress without conveying a decision, result, failure, or required action.

## Do Not Trigger When
Do not fire for structured progress required by a protocol, user-facing multi-step UX, or a single concise status at a real phase boundary.

## Distinguish From
comment-theater is non-owning comments in code; this tip is noisy status chatter in logs/messages/output.

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
Status announcements are adding noise. Report only decisions, meaningful progress, failures, and actionable results.

## Examples
### Positive
Production output, code comments, logs, or agent messages repeatedly announce routine progress without conveying a decision, result, failure, or required action.

### Near miss
A related situation that shares vocabulary but does not cross this tip's boundary — see Distinguish From.

### Counterexample
Do not fire for structured progress required by a protocol, user-facing multi-step UX, or a single concise status at a real phase boundary.
