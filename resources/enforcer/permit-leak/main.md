# permit-leak — Main

## What To Do Now
Move permit acquisition into a scoped construct that guarantees exactly one release on success, error, cancellation, and early return.

## Why This Matters
A leaked permit is capacity that disappears without evidence. Enough leaks convert bounded concurrency into eventual deadlock or starvation, often far from the operation that failed to release. The root problem is lifetime accounting hidden in control flow.

## Repair Strategy
Use `using`/`defer`/`finally`/bracket or a language-specific scoped primitive. Keep the acquired token inside that lexical lifetime and avoid transferring it unless transfer is explicit in the type/protocol.

## Decision Branches
- If release depends on reaching a later statement, wrap acquire in a scoped/bracketed construct that runs on every exit.
- If transfer is required, make the transfer linear/explicit so exactly one owner remains obligated to release.

## Common Wrong Fixes
- Add releases to known catch branches one by one; the next exit path recreates the leak.
- Release in a timeout callback while also releasing on success, causing double-release.
- Document "always release in finally" without making finally structural.

## Verification
Force exception, cancellation, timeout, and early-return paths; capacity after each must equal capacity before acquisition. The invariant is acquire/release conservation: exactly one release per acquisition.

## Done When
Every acquisition has one structurally guaranteed release and permit accounting no longer depends on manually auditing every control-flow path.
