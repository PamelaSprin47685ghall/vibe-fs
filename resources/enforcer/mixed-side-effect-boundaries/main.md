# mixed-side-effect-boundaries — Main

## What To Do Now
Separate unrelated effects behind narrow ports and move business decisions into a pure core. Keep one thin shell to sequence already-decided external operations. Each distinct effect contract is who owns that world's failure and lifetime; the pure core is who owns policy.

## Why This Matters
A database transaction, subprocess, network request, and UI update obey different notions of failure and completion. Putting them in one policy body makes every rule depend on every external lifecycle, inflating both test setup and mental state space.

## Repair Strategy
Extract domain decisions first, then isolate each effect adapter. Return typed outcomes from boundaries and compose them explicitly in the shell rather than catching/normalizing everything inside one god function.

## Decision Branches
- If the unit both decides policy and drives unrelated effects, extract the decisions to a pure core and give each effect its own port.
- If the unit only sequences already-decided commands, keep it as a thin shell and do not invent extra layers.

## Common Wrong Fixes
- Create a generic "service" interface that hides all effects behind one object.
- Move calls around without separating failure/lifetime contracts.
- Wrap mixed effects in more catch/normalize logic so the entanglement is harder to see.

## Verification
Core policy should run without external resources. Each adapter should be contract-testable independently, and orchestration should make effect ordering visible. The invariant is that unrelated effect laws do not share one policy owner.

## Done When
Domain rules no longer know storage/network/process details, and each external effect has one owner whose contract matches its actual lifecycle and failure semantics.
