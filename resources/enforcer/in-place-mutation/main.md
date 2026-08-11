# in-place-mutation — Main

## What To Do Now
Repair at the root-cause owner: who owns the shared identity must publish the transition as a new value or event, then swap the authoritative reference. Keep mutation only where no observer can see intermediate identity.

## Why This Matters
Mutation compresses “old value + transition + new value” into “whatever the object contains now.” That saves allocation but discards causal structure. Concurrency, audit, rollback, and testing then recover that structure indirectly through locks, logs, snapshots, or defensive copies.

## Repair Strategy
Define the transition as a pure function from current state and input to next state (and, where relevant, events). Swap the authoritative reference only after the transition is complete and any durability contract is satisfied.

## Decision Branches
- If another component can observe or race with the identity, produce a new value or event instead of mutating it.
- If mutation is local, unescaped, and equivalent to a pure result, keep it as an implementation detail.

## Common Wrong Fixes
- Do not clone a mutable object and then expose both copies to mutation.
- Do not add observers around field updates to simulate transactional coherence.
- Do not freeze fields after the fact while the object remains the shared identity others already mutated through.

## Verification
Old values should remain stable after a transition, intermediate states should be unobservable, and repeated reasoning about the same input state should not depend on hidden object identity. The invariant: a transition is an explicit value/fact, not a destroyed previous cell.

## Done When
State changes are explicit values/facts, while mutation—if present—has no semantic visibility beyond a narrow local scope.
