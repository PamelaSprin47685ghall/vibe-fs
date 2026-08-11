# serial-when-parallel — Enforcer

## Definition
Work is serial when it could be parallel if independent operations are forced into one temporal chain without a dependency, critical section, or protocol ordering that requires it.

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
serial-investigation is the evidence-gathering specialization. unbounded-fanout is the opposite failure: respecting independence without respecting finite capacity. This rule is general independent work chained by habit. Tie-break: fire here for implementation/tool execution graphs; fire serial-investigation when the chained work is inquiry; fire unbounded-fanout when independence is already exploited without a bound.

## Decision Procedure
Draw dependency edges. Operations with no path between them may run concurrently, subject to an explicit bound and deterministic collection of results.

## Examples
- positive: two independent HTTP GETs used to build one report are awaited in sequence for no protocol reason.
- near-miss: step B needs step A’s parsed id; serialization is the dependency, not a smell.
- counterexample: independent fetches run under a bounded pool and results are joined deterministically.

## Nudge
Let independence become concurrency, but never infinity. Execute the real dependency graph with a finite bound instead of inventing a serial chain.
