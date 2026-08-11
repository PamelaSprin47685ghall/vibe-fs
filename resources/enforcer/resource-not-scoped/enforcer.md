# resource-not-scoped — Enforcer

## Definition
A resource is not scoped when acquisition and release are separate responsibilities that control flow may accidentally separate, allowing files, processes, streams, sessions, subscriptions, worktrees, or handles to outlive their owner.

## Governing Principle
Resources are temporal values: their correctness includes when they exist and when they cease to exist. An acquire call creates an obligation whose lifetime should be visible in the same structure. If cleanup is an unrelated later action, every new return, exception, cancellation, and branch becomes another proof obligation. Lexical scoping turns lifetime from convention into syntax.

## Trigger When
Trigger when resources are opened/started/subscribed and cleanup depends on manual later calls rather than a structured lifetime mechanism.

## Do Not Trigger When
- The resource is intentionally transferred to another explicit owner with a durable lifetime contract.
- Acquisition already uses `using`/`defer`/bracket/`IDisposable`/equivalent so every exit path releases.
- The handle is a long-lived process singleton whose shutdown is the documented owner (process exit), not a missing local `close`.
- A test double does not own a real resource.

## Distinguish From
permit-leak specializes finite concurrency permits. cancellation-not-propagated concerns child work surviving owner cancellation. This rule is the general acquire/release ownership relation. Tie-break: fire here when acquire/release are not one structured lifetime; fire permit-leak when the leaked resource is a concurrency permit; fire cancellation-not-propagated when children outlive cancelled owners.

## Decision Procedure
For each acquisition, identify the owner and exact end of ownership. If release is not mechanically guaranteed at that boundary, introduce `using`/`defer`/bracket or another scoped construct.

## Examples
- positive: a function opens a temp worktree, returns early on error, and never deletes it.
- near-miss: a connection is returned to a pool whose documented owner is the pool, with `use` transferring the lease for the call.
- counterexample: `use (stream) { ... }` closes the stream on success, exception, and cancellation.

## Nudge
Make resource lifetime structural. Acquire and release under one visible owner so every exit path closes the same obligation automatically.
