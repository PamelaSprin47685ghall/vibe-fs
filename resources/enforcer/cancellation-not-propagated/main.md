# cancellation-not-propagated — Main

## What To Do Now
Thread the cancellation signal through every operation owned by the cancelled request and guarantee cleanup of resources when cancellation wins.

## Why This Matters
Returning “cancelled” while work continues is a lie about causality. It separates the visible lifecycle from the resource lifecycle: callers believe the operation ended, while processes, requests, permits, or writes remain active. Those orphan effects surface later as leaks, duplicate work, stale writes, or inexplicable contention.

## Repair Strategy
Make cancellation an explicit parameter or structured scope from the boundary inward. Adapt APIs that use different abort mechanisms, and define where ownership legitimately transfers to durable background work.

## Wrong Fixes
Do not merely ignore the eventual result. Do not catch cancellation and continue inner work. Silence at the parent does not cancel the child.

## Verification
Cancel at each meaningful phase and observe that owned external work stops, resources are released, and no later result mutates state.

## Done When
Logical and physical lifetimes agree: once an owner cancels, no work it still owns survives beyond the cancellation boundary.
