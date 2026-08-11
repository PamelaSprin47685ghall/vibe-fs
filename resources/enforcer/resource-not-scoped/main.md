# resource-not-scoped — Main

## What To Do Now
Wrap acquisition and disposal in one lexical/structured lifetime and make ownership transfer explicit where a resource legitimately escapes.

## Why This Matters
Manual cleanup makes lifetime a property of every control path. As code evolves, one new early return or cancellation path can leak a resource whose acquisition looked locally correct. Scoping reduces many path obligations to one structural guarantee.

## Repair Strategy
Use native resource constructs, keep handles inside the smallest owning scope, and compose nested resources so teardown runs deterministically in reverse ownership order.

## Wrong Fixes
Do not add cleanup calls to currently known branches one by one. That solution scales with control-flow complexity and fails again when the graph changes.

## Verification
Force success, exception, cancellation, and early exit; every acquired resource must reach its release condition exactly once.

## Done When
A reader can locate a resource’s complete lifetime from structure alone, and no exit path can abandon ownership without explicit transfer.
