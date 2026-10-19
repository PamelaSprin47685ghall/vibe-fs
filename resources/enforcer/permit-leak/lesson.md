# permit-leak — Lesson

## Opinion

### Definition

A permit leaks when a semaphore slot, lock token, lease, gate entry, or capacity right can outlive the operation that acquired it because some exit path fails to release it. The root-cause is that permit lifetime is accounted in control-flow paths instead of a scoped construct, so any unhandled exit can violate acquire/release conservation.

### Governing Principle

A permit is a linear resource: acquisition creates exactly one obligation to release. Any control flow with more than one exit threatens that conservation law unless lifetime is encoded structurally. Manual acquire/release pairs are therefore fragile not because developers forget often, but because exceptions, cancellation, and early returns continuously create new paths that must all preserve the same accounting identity.

### Trigger When

Trigger when concurrency/resource permits are acquired manually and release depends on reaching a later statement through all success, error, cancellation, and return paths.

### Do Not Trigger When

- A scoped/bracketed construct guarantees release mechanically for every exit path.
- The token is transferred by an explicit linear/type protocol that still conserves exactly one release.
- The object is not a capacity right (plain data, not a semaphore/lock/lease/gate).

### Distinguish From

resource-not-scoped concerns general acquire/dispose resources. lost-update concerns concurrent writes. Tie-break: if finite concurrency/capacity rights can leak across exit paths, this rule; if any disposable resource lacks scoped lifetime, resource-not-scoped; if concurrent writes lose a version, lost-update.

### Decision Procedure

For each acquisition, prove there is exactly one release regardless of how the scope exits. If the proof requires path-by-path inspection, move ownership into a scoped construct.

### Examples

- positive: `sem.acquire()` then `await work()` then `sem.release()`, so a thrown error keeps the slot forever.
- near-miss: `await using lease = await pool.acquire()` releases on throw, cancel, and return.
- counterexample: A mutex used via `with lock:` / `defer unlock` with no manual pairing.

## Advice

### What To Do Now

Move permit acquisition into a scoped construct that guarantees exactly one release on success, error, cancellation, and early return. The scoped acquire/release construct (using/defer/finally/bracket) is who owns the conservation invariant that every acquisition has exactly one release on every exit.

### Why This Matters

A leaked permit is capacity that disappears without evidence. Enough leaks convert bounded concurrency into eventual deadlock or starvation, often far from the operation that failed to release. The root problem is lifetime accounting hidden in control flow.

### Repair Strategy

Use `using`/`defer`/`finally`/bracket or a language-specific scoped primitive. Keep the acquired token inside that lexical lifetime and avoid transferring it unless transfer is explicit in the type/protocol.

### Decision Branches

- If release depends on reaching a later statement, wrap acquire in a scoped/bracketed construct that runs on every exit.
- If transfer is required, make the transfer linear/explicit so exactly one owner remains obligated to release.

### Common Wrong Fixes

- Add releases to known catch branches one by one; the next exit path recreates the leak.
- Release in a timeout callback while also releasing on success, causing double-release.
- Document "always release in finally" without making finally structural.

### Verification

Force exception, cancellation, timeout, and early-return paths; capacity after each must equal capacity before acquisition. The invariant is acquire/release conservation: exactly one release per acquisition.

### Done When

Every acquisition has one structurally guaranteed release and permit accounting no longer depends on manually auditing every control-flow path.

### Nudge

Treat permits as linear values: acquire once, release exactly once. Let lexical scope enforce that conservation law instead of relying on every exit path to remember it.
