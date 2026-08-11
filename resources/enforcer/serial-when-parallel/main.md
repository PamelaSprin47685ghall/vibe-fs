# serial-when-parallel — Main

## What To Do Now
Run independent operations concurrently under a clear finite bound and preserve ordering only where the data or external protocol requires it. The real dependency graph, together with that declared capacity, is who owns the schedule; sequential habit is not.

## Why This Matters
Artificial serialization lengthens the critical path while communicating a dependency that does not exist. Correct concurrency is architectural compression: it makes elapsed time reflect the longest true dependency chain rather than the sum of unrelated waits.

## Repair Strategy
Partition work by dependency, choose a capacity bound from the resource being protected, propagate cancellation, and gather outcomes deterministically so concurrency does not leak scheduler order into semantics.

## Decision Branches
- If operations share no data or owner, overlap them under a finite bound and join deterministically.
- If later work needs earlier results or a protocol order, keep that edge serial.
- If the current code already overlaps without a bound, that is `unbounded-fanout`—add capacity, do not re-serialize everything.

## Common Wrong Fixes
- Do not replace serialization with unbounded `all` or spawn behavior.
- Do not parallelize steps that share mutable state without an owner.
- Do not keep sequential `await` “for readability” when the graph is independent.
- Do not let completion order become the merge rule (see `race-first-wins-semantics`).

## Verification
Reordering completion of independent operations must not change the logical result, and active concurrency must never exceed the chosen bound. The invariant is that the schedule matches the dependency graph and a declared capacity.

## Done When
The schedule expresses two facts honestly: dependent work waits, independent work overlaps, and finite resources remain explicitly bounded.
