# serial-when-parallel — Enforcer

## Definition
Work is serial when it could be parallel if independent operations are forced into one temporal chain without a dependency, critical section, or protocol ordering that requires it. The root-cause is that independence is ignored and a serial chain is invented, so elapsed time becomes the sum of unrelated waits rather than the longest true dependency.

## Governing Principle
Concurrency should follow independence. If A and B share no premise or mutable owner, making B wait for A invents causality the problem does not contain. The opposite error is unbounded fan-out; the correct model is a dependency graph executed with finite capacity.

## Trigger When
Trigger when independent tool calls, validations, reads, computations, or I/O operations run sequentially solely by implementation habit.

## Do Not Trigger When
- Later work consumes earlier results, so a data dependency is real.
- Shared mutable state requires serialization.
- An external protocol defines order as part of correctness.
- The operations are already concurrent under an explicit finite bound.

## Distinguish From
`serial-investigation` is the evidence-gathering specialization. `unbounded-fanout` respects independence without respecting finite capacity. `blocking-event-loop` monopolizes the shared progress engine. `big-batch-intent` fuses independent success conditions before scheduling. This rule is general independent work chained by habit. Tie-break: if the chained work is implementation or tool execution rather than inquiry, this rule owns the case.

## Decision Procedure
1. Draw dependency edges from data, ownership, and protocol order.
2. Treat operations with no path between them as concurrent candidates.
3. Choose an explicit finite bound from the resource being protected.
4. Join results deterministically so scheduler order does not become semantics.

## Examples
- positive: two independent HTTP GETs used to build one report are awaited in sequence for no protocol reason.
- near-miss: step B needs step A’s parsed id; serialization is the dependency, not a smell.
- counterexample: independent fetches run under a bounded pool and results are joined deterministically.

## Nudge
Let independence become concurrency, but never infinity. Execute the real dependency graph with a finite bound instead of inventing a serial chain.
