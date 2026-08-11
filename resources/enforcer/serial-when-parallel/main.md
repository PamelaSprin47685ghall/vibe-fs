# serial-when-parallel — Main

## What To Do Now
Identify independent units, run them concurrently with a bound, and merge results in a deterministic order for downstream logic.

## Repair Strategy
Draw the dependency graph. Parallelize disconnected nodes. Add a semaphore or bounded map. Define how partial failures combine.

## Decision Branches
If side effects conflict, serialize only the conflicting section. If result order matters for UX, parallelize execution but present deterministically.

## Wrong Fixes
Awaiting each independent promise in sequence. Spawning unbounded tasks as the "fix". Relying on race order for merge (see race-first-wins-semantics).

## Verification
Independent units overlap in time under a bound; outputs remain deterministic across runs.

## Done When
Independent work runs concurrently within bounds; serial sections are justified by real dependencies.

## Scope and Authority
Runtime and agent tool orchestration for independent units of work.
