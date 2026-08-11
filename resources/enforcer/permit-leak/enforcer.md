# permit-leak — Enforcer

## Definition
A permit leaks when a semaphore slot, lock token, lease, gate entry, or capacity right can outlive the operation that acquired it because some exit path fails to release it.

## Governing Principle
A permit is a linear resource: acquisition creates exactly one obligation to release. Any control flow with more than one exit threatens that conservation law unless lifetime is encoded structurally. Manual acquire/release pairs are therefore fragile not because developers forget often, but because exceptions, cancellation, and early returns continuously create new paths that must all preserve the same accounting identity.

## Trigger When
Trigger when concurrency/resource permits are acquired manually and release depends on reaching a later statement through all success, error, cancellation, and return paths.

## Do Not Trigger When
Do not trigger when a scoped/bracketed construct guarantees release mechanically for every exit path.

## Distinguish From
resource-not-scoped concerns general acquire/dispose resources. lost-update concerns concurrent writes. This rule is specifically conservation of finite concurrency/capacity rights.

## Decision Procedure
For each acquisition, prove there is exactly one release regardless of how the scope exits. If the proof requires path-by-path inspection, move ownership into a scoped construct.

## Nudge
Treat permits as linear values: acquire once, release exactly once. Let lexical scope enforce that conservation law instead of relying on every exit path to remember it.
