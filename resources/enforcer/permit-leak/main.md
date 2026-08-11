# permit-leak — Main

## What To Do Now
Move permit acquisition into a scoped construct that guarantees exactly one release on success, error, cancellation, and early return.

## Why This Matters
A leaked permit is capacity that disappears without evidence. Enough leaks convert bounded concurrency into eventual deadlock or starvation, often far from the operation that failed to release. The root problem is lifetime accounting hidden in control flow.

## Repair Strategy
Use `using`/`defer`/`finally`/bracket or a language-specific scoped primitive. Keep the acquired token inside that lexical lifetime and avoid transferring it unless transfer is explicit in the type/protocol.

## Wrong Fixes
Do not add releases to known catch branches one by one. The next exit path recreates the leak. Structure should guarantee the accounting identity globally.

## Verification
Force exception, cancellation, timeout, and early-return paths; capacity after each must equal capacity before acquisition.

## Done When
Every acquisition has one structurally guaranteed release and permit accounting no longer depends on manually auditing every control-flow path.
