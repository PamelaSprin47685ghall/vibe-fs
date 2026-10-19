# serial-when-parallel — Lesson

## Opinion

### Definition

Work is serial when it could be parallel if independent operations are forced into one temporal chain without a dependency, critical section, or protocol ordering that requires it. The root-cause is that independence is ignored and a serial chain is invented, so elapsed time becomes the sum of unrelated waits rather than the longest true dependency.

### Governing Principle

Concurrency should follow independence. If A and B share no premise or mutable owner, making B wait for A invents causality the problem does not contain. The opposite error is unbounded fan-out; the correct model is a dependency graph executed with finite capacity.

### Trigger When

Trigger when independent tool calls, validations, reads, computations, or I/O operations run sequentially solely by implementation habit.

### Do Not Trigger When

- Later work consumes earlier results, so a data dependency is real.
- Shared mutable state requires serialization.
- An external protocol defines order as part of correctness.
- The operations are already concurrent under an explicit finite bound.

### Distinguish From

`serial-investigation` is the evidence-gathering specialization. `unbounded-fanout` respects independence without respecting finite capacity. `blocking-event-loop` monopolizes the shared progress engine. `big-batch-intent` fuses independent success conditions before scheduling. This rule is general independent work chained by habit. Tie-break: if the chained work is implementation or tool execution rather than inquiry, this rule owns the case.

### Decision Procedure

1. Draw dependency edges from data, ownership, and protocol order.
2. Treat operations with no path between them as concurrent candidates.
3. Choose an explicit finite bound from the resource being protected.
4. Join results deterministically so scheduler order does not become semantics.

### Examples

- positive: two independent HTTP GETs used to build one report are awaited in sequence for no protocol reason.
- near-miss: step B needs step A’s parsed id; serialization is the dependency, not a smell.
- counterexample: independent fetches run under a bounded pool and results are joined deterministically.

## Advice

### What To Do Now

Run independent operations concurrently under a clear finite bound and preserve ordering only where the data or external protocol requires it. The real dependency graph, together with that declared capacity, is who owns the schedule; sequential habit is not.

### Why This Matters

Artificial serialization lengthens the critical path while communicating a dependency that does not exist. Correct concurrency is architectural compression: it makes elapsed time reflect the longest true dependency chain rather than the sum of unrelated waits.

### Repair Strategy

Partition work by dependency, choose a capacity bound from the resource being protected, propagate cancellation, and gather outcomes deterministically so concurrency does not leak scheduler order into semantics.

### Decision Branches

- If operations share no data or owner, overlap them under a finite bound and join deterministically.
- If later work needs earlier results or a protocol order, keep that edge serial.
- If the current code already overlaps without a bound, that is `unbounded-fanout`—add capacity, do not re-serialize everything.

### Common Wrong Fixes

- Do not replace serialization with unbounded `all` or spawn behavior.
- Do not parallelize steps that share mutable state without an owner.
- Do not keep sequential `await` “for readability” when the graph is independent.
- Do not let completion order become the merge rule (see `race-first-wins-semantics`).

### Verification

Reordering completion of independent operations must not change the logical result, and active concurrency must never exceed the chosen bound. The invariant is that the schedule matches the dependency graph and a declared capacity.

### Done When

The schedule expresses two facts honestly: dependent work waits, independent work overlaps, and finite resources remain explicitly bounded.

### Nudge

Let independence become concurrency, but never infinity. Execute the real dependency graph with a finite bound instead of inventing a serial chain.
