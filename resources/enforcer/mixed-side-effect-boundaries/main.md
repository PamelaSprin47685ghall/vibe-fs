# mixed-side-effect-boundaries — Main

## What To Do Now
Separate unrelated effects behind narrow ports and move business decisions into a pure core. Keep one thin shell to sequence already-decided external operations.

## Why This Matters
A database transaction, subprocess, network request, and UI update obey different notions of failure and completion. Putting them in one policy body makes every rule depend on every external lifecycle, inflating both test setup and mental state space.

## Repair Strategy
Extract domain decisions first, then isolate each effect adapter. Return typed outcomes from boundaries and compose them explicitly in the shell rather than catching/normalizing everything inside one god function.

## Wrong Fixes
Do not create a generic “service” interface that merely hides all effects behind one object. One abstraction over unrelated failure laws is still one mixed boundary.

## Verification
Core policy should run without external resources. Each adapter should be contract-testable independently, and orchestration should make effect ordering visible.

## Done When
Domain rules no longer know storage/network/process details, and each external effect has one owner whose contract matches its actual lifecycle and failure semantics.
