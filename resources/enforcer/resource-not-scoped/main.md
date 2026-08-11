# resource-not-scoped — Main

## What To Do Now
Wrap acquisition and disposal in one lexical/structured lifetime and make ownership transfer explicit where a resource legitimately escapes.

## Why This Matters
Manual cleanup makes lifetime a property of every control path. As code evolves, one new early return or cancellation path can leak a resource whose acquisition looked locally correct. Scoping reduces many path obligations to one structural guarantee.

## Repair Strategy
Use native resource constructs, keep handles inside the smallest owning scope, and compose nested resources so teardown runs deterministically in reverse ownership order.

## Decision Branches
- If the handle must not escape the function, wrap acquire/release in one scoped construct.
- If ownership must move, encode transfer in the type/API so the receiver becomes the sole releaser.
- If cleanup is currently sprinkled on known branches, replace the branches with one owner rather than adding more `close` calls.

## Common Wrong Fixes
- Add cleanup calls to currently known branches one by one.
- Swallow dispose errors so the scope “looks” complete.
- Rely on GC finalizers as the primary release path.
- Share a handle globally “so someone will close it later.”

## Verification
Force success, exception, cancellation, and early exit; every acquired resource must reach its release condition exactly once. The invariant is: acquisition creates an obligation that structure, not path enumeration, discharges.

## Done When
A reader can locate a resource’s complete lifetime from structure alone, and no exit path can abandon ownership without explicit transfer.
