# resource-not-scoped — Enforcer

## Definition
Files, processes, streams, sessions, subscriptions, worktrees, or handles are acquired without a deterministic lifetime that pairs disposal.

## Trigger When
Files, processes, streams, sessions, subscriptions, worktrees, or handles are not tied to a deterministic lifetime.

## Do Not Trigger When
Do not fire when ownership is already structured (use/using/defer/bracket) with tests for cleanup on success and failure.

## Distinguish From
permit-leak is authorization lifetime; unbounded-fanout is spawn volume; this tip is missing acquire/release pairing.

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
A resource lacks scoped ownership. Make acquisition and disposal part of one structured lifetime.

## Examples
### Positive
Files, processes, streams, sessions, subscriptions, worktrees, or handles are not tied to a deterministic lifetime.

### Near miss
A related situation that shares vocabulary but does not cross this tip's boundary — see Distinguish From.

### Counterexample
Do not fire when ownership is already structured (use/using/defer/bracket) with tests for cleanup on success and failure.
