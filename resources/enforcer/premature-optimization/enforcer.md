# premature-optimization — Enforcer

## Definition
Optimization is premature when complexity is introduced to improve performance before measurement or an explicit resource constraint establishes that the simpler design is insufficient.

## Governing Principle
Optimization trades one resource for another. Often it spends readability, determinism, memory model simplicity, or architectural freedom to buy latency or throughput. Without evidence that the purchased resource is scarce, the trade has no denominator and cannot be evaluated. The system pays certain complexity for hypothetical performance.

## Trigger When
Trigger when caching, pooling, batching, custom data structures, concurrency, unsafe mutation, denormalization, or low-level tricks are introduced without a measured bottleneck or stated hard budget.

## Do Not Trigger When
- An external SLO, memory limit, algorithmic bound, or profiling result demonstrates the simple implementation cannot meet the required constraint.
- The change is a known-complexity algorithm required by the problem size already in production.
- The "optimization" is removing accidental work with no added machinery.

## Distinguish From
incidental-complexity-dominates is the resulting design condition. dependency-bloat imports machinery. Tie-break: if speculative performance tradeoffs precede evidence, this rule; if the design is already dominated by accidental complexity, incidental-complexity-dominates; if unused libraries were pulled in, dependency-bloat.

## Decision Procedure
State the target metric and budget, measure the simple design, locate the dominant cost, and optimize only that cost. Re-measure afterward to prove the complexity purchased the required improvement.

## Examples
- positive: A custom lock-free cache is added "for scale" with no profile and a workload of hundreds of requests per day.
- near-miss: Profiling shows 90% of latency in one serialization path, and that path is then specialized against a stated SLO.
- counterexample: The simple design already meets the budget; no extra caching or pooling is introduced.

## Nudge
Performance work is an evidence-based trade. Keep the simple design until a measured constraint names what must be faster, smaller, or more scalable.
