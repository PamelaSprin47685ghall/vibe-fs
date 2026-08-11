# unbounded-fanout — Enforcer

## Definition
Fan-out is unbounded when work creation follows input size without a finite concurrency limit, allowing tasks, requests, processes, agents, or file operations to demand resources faster than the system can service them.

## Governing Principle
Concurrency converts a collection into simultaneous claims on finite resources. Without a bound, input cardinality becomes resource policy by accident: a larger list means more sockets, memory, file descriptors, processes, or remote pressure with no independent ceiling. Bounded concurrency separates two quantities that must not be confused—the amount of work that exists and the amount of work allowed to be active now.

## Trigger When
Trigger when code spawns or starts one concurrent operation per item with no semaphore, worker pool, bounded map, queue capacity, or inherently small fixed upper bound.

## Do Not Trigger When
- The fan-out is statically tiny and documented (a fixed handful of known children).
- An existing mechanism enforces a finite active-work limit with cancellation semantics.
- Work is processed sequentially or through a pool whose size is independent of input cardinality.
- The “fan-out” is pure CPU iteration with no extra concurrent resource claims.

## Distinguish From
`serial-when-parallel` underuses available concurrency. `resource-not-scoped` concerns lifetime after acquisition. Tie-break: if simultaneous demand has no upper bound, use this rule; if work that could be parallel is forced serial, use `serial-when-parallel`.

## Decision Procedure
Identify the finite resource exhausted by concurrency, choose an explicit safe active bound, and define what happens to queued work when the parent fails or cancels.

## Examples
- positive: `items.map(i => fetch(i))` awaits all at once with no concurrency cap.
- near-miss: a worker pool of size 8 pulls from a queue of 10,000 jobs with cancellation on parent abort.
- counterexample: a pipeline that could fetch in parallel but loops `await` one-by-one is `serial-when-parallel`.

## Nudge
Input size is not a concurrency policy. Separate total work from active work with a finite bound, and make cancellation of queued and running children explicit.
