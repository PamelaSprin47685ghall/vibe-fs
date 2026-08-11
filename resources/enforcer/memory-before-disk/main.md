# memory-before-disk — Main

## What To Do Now
Reorder the write path so the durable fact commits first; only after success may authoritative memory advance and downstream observers see the new state.

## Why This Matters
A process can lie to itself before it crashes. If memory changes first, subsequent commands may act on a state that restart cannot recover, creating a split between the world that influenced behavior and the world that left evidence.

## Repair Strategy
Compute the intended transition without mutating authority, append/commit the durable fact, then derive and swap the new in-memory state. Treat durable failure as “the command did not happen.”

## Wrong Fixes
Do not mutate memory and attempt to roll it back if persistence fails; rollback itself becomes another failure path and cannot erase effects already observed by other work.

## Verification
Inject failure before and during durable commit. Memory and observers must remain at the old state. After successful commit, crash/restart replay must reconstruct exactly the state that was exposed.

## Done When
No authoritative runtime state can get ahead of the durable evidence from which recovery derives it.
