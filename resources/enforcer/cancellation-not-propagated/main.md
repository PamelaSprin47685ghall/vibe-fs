# cancellation-not-propagated — Main

## What To Do Now
Thread the cancellation signal through every operation owned by the cancelled request and guarantee cleanup of resources when cancellation wins. The cancelled parent is who owns every still-owned child lifetime; abort must reach those children, or ownership must transfer before detach.

## Why This Matters
Returning “cancelled” while work continues is a lie about causality. It separates the visible lifecycle from the resource lifecycle: callers believe the operation ended, while processes, requests, permits, or writes remain active. Those orphan effects surface later as leaks, duplicate work, stale writes, or inexplicable contention.

## Repair Strategy
Make cancellation an explicit parameter or structured scope from the boundary inward. Adapt APIs that use different abort mechanisms, and define where ownership legitimately transfers to durable background work.

## Decision Branches
- If child work is still owned by the cancelled parent, propagate abort through every child and await cleanup.
- If the work must outlive the request, transfer ownership to an explicit durable principal before the parent exits.
- If an API uses a different abort mechanism, adapt it at the edge rather than dropping the signal.

## Common Wrong Fixes
- Do not merely ignore the eventual result after returning cancelled.
- Do not catch cancellation and continue inner work.
- Do not close only the outer socket while inner processes keep running.
- Do not treat a timeout on the caller as cancellation of unthreaded children.

## Verification
Cancel at each meaningful phase and observe that owned external work stops, resources are released, and no later result mutates state. The invariant is that logical and physical lifetimes agree: once an owner cancels, no work it still owns survives beyond the cancellation boundary.

## Done When
Logical and physical lifetimes agree: once an owner cancels, no work it still owns survives beyond the cancellation boundary.
