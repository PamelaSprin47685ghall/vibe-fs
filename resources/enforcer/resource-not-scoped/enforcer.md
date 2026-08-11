# resource-not-scoped — Enforcer

## Definition
A resource is not scoped when acquisition and release are separate responsibilities that control flow may accidentally separate, allowing files, processes, streams, sessions, subscriptions, worktrees, or handles to outlive their owner.

## Governing Principle
Resources are temporal values: their correctness includes when they exist and when they cease to exist. An acquire call creates an obligation whose lifetime should be visible in the same structure. If cleanup is an unrelated later action, every new return, exception, cancellation, and branch becomes another proof obligation. Lexical scoping turns lifetime from convention into syntax.

## Trigger When
Trigger when resources are opened/started/subscribed and cleanup depends on manual later calls rather than a structured lifetime mechanism.

## Do Not Trigger When
Do not trigger when the resource is intentionally transferred to another explicit owner with a durable lifetime contract.

## Distinguish From
permit-leak specializes finite concurrency permits. cancellation-not-propagated concerns child work surviving owner cancellation. This rule is the general acquire/release ownership relation.

## Decision Procedure
For each acquisition, identify the owner and exact end of ownership. If release is not mechanically guaranteed at that boundary, introduce `using`/`defer`/bracket or another scoped construct.

## Nudge
Make resource lifetime structural. Acquire and release under one visible owner so every exit path closes the same obligation automatically.
