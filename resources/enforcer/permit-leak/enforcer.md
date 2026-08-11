# permit-leak — Enforcer

## Definition
A permit leaks when a semaphore slot, lock token, lease, gate entry, or capacity right can outlive the operation that acquired it because some exit path fails to release it. The root-cause is that permit lifetime is accounted in control-flow paths instead of a scoped construct, so any unhandled exit can violate acquire/release conservation.

## Governing Principle
A permit is a linear resource: acquisition creates exactly one obligation to release. Any control flow with more than one exit threatens that conservation law unless lifetime is encoded structurally. Manual acquire/release pairs are therefore fragile not because developers forget often, but because exceptions, cancellation, and early returns continuously create new paths that must all preserve the same accounting identity.

## Trigger When
Trigger when concurrency/resource permits are acquired manually and release depends on reaching a later statement through all success, error, cancellation, and return paths.

## Do Not Trigger When
- A scoped/bracketed construct guarantees release mechanically for every exit path.
- The token is transferred by an explicit linear/type protocol that still conserves exactly one release.
- The object is not a capacity right (plain data, not a semaphore/lock/lease/gate).

## Distinguish From
resource-not-scoped concerns general acquire/dispose resources. lost-update concerns concurrent writes. Tie-break: if finite concurrency/capacity rights can leak across exit paths, this rule; if any disposable resource lacks scoped lifetime, resource-not-scoped; if concurrent writes lose a version, lost-update.

## Decision Procedure
For each acquisition, prove there is exactly one release regardless of how the scope exits. If the proof requires path-by-path inspection, move ownership into a scoped construct.

## Examples
- positive: `sem.acquire()` then `await work()` then `sem.release()`, so a thrown error keeps the slot forever.
- near-miss: `await using lease = await pool.acquire()` releases on throw, cancel, and return.
- counterexample: A mutex used via `with lock:` / `defer unlock` with no manual pairing.

## Nudge
Treat permits as linear values: acquire once, release exactly once. Let lexical scope enforce that conservation law instead of relying on every exit path to remember it.
