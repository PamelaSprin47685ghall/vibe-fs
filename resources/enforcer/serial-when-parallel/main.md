# serial-when-parallel — Main

## What To Do Now
Run independent operations concurrently under a clear finite bound and preserve ordering only where the data or external protocol requires it.

## Why This Matters
Artificial serialization lengthens the critical path while communicating a dependency that does not exist. Correct concurrency is architectural compression: it makes elapsed time reflect the longest true dependency chain rather than the sum of unrelated waits.

## Repair Strategy
Partition work by dependency, choose a capacity bound from the resource being protected, propagate cancellation, and gather outcomes deterministically so concurrency does not leak scheduler order into semantics.

## Wrong Fixes
Do not replace serialization with unbounded `all`/spawn behavior. Independence justifies overlap, not unlimited resource demand.

## Verification
Reordering completion of independent operations must not change the logical result, and active concurrency must never exceed the chosen bound.

## Done When
The schedule expresses two facts honestly: dependent work waits, independent work overlaps, and finite resources remain explicitly bounded.
