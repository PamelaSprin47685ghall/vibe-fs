# unbounded-fanout — Enforcer

## Definition
Fan-out is unbounded when work creation follows input size without a finite concurrency limit, allowing tasks, requests, processes, agents, or file operations to demand resources faster than the system can service them.

## Governing Principle
Concurrency converts a collection into simultaneous claims on finite resources. Without a bound, input cardinality becomes resource policy by accident: a larger list means more sockets, memory, file descriptors, processes, or remote pressure with no independent ceiling. Bounded concurrency separates two quantities that must not be confused—the amount of work that exists and the amount of work allowed to be active now.

## Trigger When
Trigger when code spawns or starts one concurrent operation per item with no semaphore, worker pool, bounded map, queue capacity, or inherently small fixed upper bound.

## Do Not Trigger When
Do not trigger when the fan-out is statically tiny and documented, or an existing mechanism enforces a finite active-work limit with cancellation semantics.

## Distinguish From
serial-when-parallel underuses available concurrency. resource-not-scoped concerns lifetime after acquisition. This rule concerns absence of an upper bound on simultaneous demand.

## Decision Procedure
Identify the finite resource exhausted by concurrency, choose an explicit safe active bound, and define what happens to queued work when the parent fails or cancels.

## Nudge
Input size is not a concurrency policy. Separate total work from active work with a finite bound, and make cancellation of queued and running children explicit.
