# serial-investigation — Main

## What To Do Now
Batch independent reads and searches in one turn. Synthesize after all evidence arrives. Only chain steps that need prior outputs.

## Repair Strategy
List the unknown questions. Mark dependency edges. Parallelize the independent set. Keep a short serial tail for dependent refinement.

## Decision Branches
If the first hit may eliminate the rest, a cheap serial probe is fine—then parallelize the remainder. If tools rate-limit, bound concurrency rather than going fully serial by habit.

## Wrong Fixes
Reading one file per turn out of habit. Waiting for irrelevant context before starting independent searches. Serializing purely to narrate progress.

## Verification
Independent lookups were issued together; wall time reflects overlap, not a chain of waits.

## Done When
Investigation plan shows concurrent independent reads; only true dependencies remain serial.

## Scope and Authority
Research and diagnostics workflow. Not user-visible product request handling (see serial-when-parallel).
